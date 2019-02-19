/*
General purpose Dll injection tool - Oguz Kartal 2018

*/

//TODO: set SizeOfImage 

#define _CRT_SECURE_NO_WARNINGS 1
#define WIN32_LEAN_AND_MEAN

#include <Windows.h>
#include <stdio.h>
#include <stdlib.h>

CHAR                        HkpImagePath[MAX_PATH];
HANDLE                      HkpPeFileHandle,HkpMappingHandle;
DWORD                       HkpImageBase = NULL;
USHORT                      HkpImportModuleCount = 0;
SHORT                       HkpInjectOrder = -1;
BOOL                        HkpBuildOFT = FALSE;
PIMAGE_NT_HEADERS           HkpNtHeader = NULL;
PIMAGE_SECTION_HEADER       HkpSections = NULL;
PIMAGE_IMPORT_DESCRIPTOR    HkpImports = NULL;

#define PTR2ADDR(ptr) ((DWORD)ptr)
#define ADDR2OFF(addr) (((DWORD)addr) - HkpImageBase)
#define RVA2OFF(rva) HkpRvaToOffset(rva)
#define OFF2RVA(off) HkpOffsetToRva(off)

#ifdef _DEBUG
#define DBGPRINT 1
#endif

#if DBGPRINT
#define xdbgp(format,...) printf("[dbg] " format "\n",__VA_ARGS__)
#else
#define xdbgp(format,...)
#endif

#define xwr(format, ...) printf("[+] " format "\n", __VA_ARGS__)
#define wr(format, ...) printf(format,__VA_ARGS__)

PVOID HkpAlloc(ULONG size)
{
    return HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, size);
}

PVOID HkpRealloc(PVOID old, ULONG size)
{
    if (!old)
        return HkpAlloc(size);

    return HeapReAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, old, size);
}

void HkpFree(PVOID mem)
{
    HeapFree(GetProcessHeap(), 0, mem);
}

PIMAGE_SECTION_HEADER HkpGetSectionByRva(DWORD rva)
{
    PIMAGE_SECTION_HEADER psect = HkpSections;
        
    for (LONG i = 0; i < HkpNtHeader->FileHeader.NumberOfSections; i++)
    {
        if (rva >= psect->VirtualAddress && rva <= psect->VirtualAddress + psect->Misc.VirtualSize)
        {
            return psect;
        }

        psect++;
    }

    return NULL;
}

PIMAGE_SECTION_HEADER HkpGetSectionByOffset(DWORD offset)
{
    PIMAGE_SECTION_HEADER psect = HkpSections;

    for (LONG i = 0; i < HkpNtHeader->FileHeader.NumberOfSections; i++)
    {
        if (offset >= psect->PointerToRawData && offset <= psect->PointerToRawData + psect->SizeOfRawData)
        {
            return psect;
        }
    }
}

DWORD HkpRvaToOffset(DWORD rva)
{
    PIMAGE_SECTION_HEADER psect;
    DWORD diff;

    psect = HkpGetSectionByRva(rva);

    if (!psect)
        return 0;

    diff = rva - psect->VirtualAddress;

    return psect->PointerToRawData + diff;
}

DWORD HkpOffsetToRva(DWORD offset)
{
    PIMAGE_SECTION_HEADER psect;
    DWORD diff;

    psect = HkpGetSectionByOffset(offset);

    if (!psect)
        return 0;

    diff = offset - psect->PointerToRawData;

    return psect->VirtualAddress + diff;
}

PIMAGE_NT_HEADERS HkpLocateNtHeaders()
{
    PIMAGE_DOS_HEADER pDosHdr;
    PIMAGE_NT_HEADERS pNtHdr;

    pDosHdr = HkpImageBase;

    if (pDosHdr->e_magic != IMAGE_DOS_SIGNATURE)
    {
        xwr("invalid image\n");
        return NULL;
    }

    pNtHdr = HkpImageBase + pDosHdr->e_lfanew;

    if (pNtHdr->Signature != IMAGE_NT_SIGNATURE)
    {
        xwr("invalid image\n");
        return NULL;
    }

    if (pNtHdr->FileHeader.Machine != IMAGE_FILE_MACHINE_I386)
    {
        xwr("invalid cpu architecture\n");
        return NULL;
    }


    HkpNtHeader = pNtHdr;

    return pNtHdr;
}



void HkpLocateSections()
{
    PIMAGE_SECTION_HEADER psect = NULL;

    if (!HkpSections)
        HkpSections = PTR2ADDR(HkpNtHeader) + sizeof(IMAGE_NT_HEADERS);

    psect = HkpSections;

    for (LONG i = 0; i < HkpNtHeader->FileHeader.NumberOfSections; i++)
    {
        psect++;
    }
}

void HkpLocateImports()
{
    PIMAGE_DATA_DIRECTORY pImportDir;

    pImportDir = &HkpNtHeader->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];

    xwr("Import directory VA %x, OFFSET %x\n", pImportDir->VirtualAddress, RVA2OFF(pImportDir->VirtualAddress));

    HkpImports = HkpImageBase + RVA2OFF(pImportDir->VirtualAddress);

    for (PIMAGE_IMPORT_DESCRIPTOR p = HkpImports;; p++)
    {
        if (p->FirstThunk == 0 && p->OriginalFirstThunk == 0)
            break;

        HkpImportModuleCount++;
    }
}


PVOID HkLoadPE(PCHAR peFileName)
{
    LARGE_INTEGER fsize;

    
    HkpPeFileHandle = CreateFileA(peFileName, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    
    if (HkpPeFileHandle == INVALID_HANDLE_VALUE)
        return NULL;

    GetFileSizeEx(HkpPeFileHandle, &fsize);

    HkpMappingHandle = CreateFileMappingA(HkpPeFileHandle, NULL, PAGE_READWRITE, 0, fsize.LowPart, NULL);

    if (!HkpMappingHandle)
    {
        CloseHandle(HkpPeFileHandle);
        return NULL;
    }
    
    HkpImageBase = (DWORD)MapViewOfFile(HkpMappingHandle, FILE_MAP_ALL_ACCESS, 0, 0, (SIZE_T)fsize.QuadPart);

    if (!HkpImageBase)
    {
        CloseHandle(HkpMappingHandle);
        CloseHandle(HkpPeFileHandle);
        return NULL;
    }

    HkpLocateNtHeaders();
    HkpLocateSections();
    HkpLocateImports();

    return HkpImageBase;
}

BOOL HkUnmapPE()
{
    if (!UnmapViewOfFile((LPCVOID)HkpImageBase))
    {
        return FALSE;
    }

    CloseHandle(HkpMappingHandle);

    HkpMappingHandle = NULL;

    return TRUE;
}

BOOL HkUnloadPE()
{
    if (HkpMappingHandle)
    {
        if (!HkUnmapPE())
            return FALSE;
    }

    CloseHandle(HkpPeFileHandle);

    return TRUE;
}



PIMAGE_SECTION_HEADER HkpGetLastSection()
{
    return HkpSections + (HkpNtHeader->FileHeader.NumberOfSections-1);
}



#define ALIGNTO(x,align) ((((x) / (align)) + 1) * (align))
#define abs(x) x < 0 ? -x : x


__forceinline DWORD HkpLocalAddressToSectionAppendingOffset(DWORD localBase, DWORD localAddr, PIMAGE_SECTION_HEADER section)
{
    DWORD sectionEnd = section->PointerToRawData + section->SizeOfRawData;
    DWORD diff = localAddr - localBase;

    return sectionEnd + diff;
}

__forceinline DWORD HkpLocalAddressToSectionAppendingRVA(DWORD localBase, DWORD localAddr, PIMAGE_SECTION_HEADER section)
{
    DWORD sectionEndRva = section->Misc.VirtualSize + section->VirtualAddress;
    DWORD diff = localAddr - localBase;

    return sectionEndRva + diff;
}

BOOL HkpWriteToPE(DWORD newImportDirectory, PBYTE data, ULONG size, DWORD newVirtualSize, DWORD newRawSize)
{
    DWORD dummy = 0, fp = 0;
    IMAGE_DOS_HEADER dosHeader;
    IMAGE_NT_HEADERS ntHeader;
    IMAGE_SECTION_HEADER lastSection;
    LARGE_INTEGER fileSize;

    GetFileSizeEx(HkpPeFileHandle, &fileSize);

    ReadFile(HkpPeFileHandle, &dosHeader, sizeof(IMAGE_DOS_HEADER), &dummy, NULL);
    SetFilePointer(HkpPeFileHandle, dosHeader.e_lfanew, 0, FILE_BEGIN);
    
    ReadFile(HkpPeFileHandle, &ntHeader, sizeof(IMAGE_NT_HEADERS), &dummy, NULL);

    ntHeader.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress = newImportDirectory;
    ntHeader.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].Size = size;

    SetFilePointer(HkpPeFileHandle, dosHeader.e_lfanew, 0, FILE_BEGIN);
    WriteFile(HkpPeFileHandle, &ntHeader, sizeof(IMAGE_NT_HEADERS), &dummy, NULL);

    
    fp = ((ntHeader.FileHeader.NumberOfSections - 1) * sizeof(IMAGE_SECTION_HEADER));

    SetFilePointer(HkpPeFileHandle, fp, 0, FILE_CURRENT);
    ReadFile(HkpPeFileHandle, &lastSection, sizeof(IMAGE_SECTION_HEADER), &dummy, NULL);

    lastSection.Misc.VirtualSize = newVirtualSize;
    lastSection.SizeOfRawData = newRawSize;

    SetFilePointer(HkpPeFileHandle, -((LONG)sizeof(IMAGE_SECTION_HEADER)), 0, FILE_CURRENT);
    WriteFile(HkpPeFileHandle, &lastSection, sizeof(IMAGE_SECTION_HEADER), &dummy, NULL);

    SetFilePointer(HkpPeFileHandle, fileSize.LowPart, &fileSize.HighPart, FILE_BEGIN);
    
    WriteFile(HkpPeFileHandle, data, size, &dummy, NULL);

    return TRUE;

}

BOOL HkpGetThunksFromImportDescriptor(PIMAGE_IMPORT_DESCRIPTOR pDesc, DWORD **pThunks, ULONG *pCount)
{
    BOOL justQueryCount=FALSE;
    DWORD size = 0;
    DWORD *thunks = NULL;
    ULONG count = 0;
    PIMAGE_THUNK_DATA thunk = (PIMAGE_THUNK_DATA)(HkpImageBase + RVA2OFF(pDesc->FirstThunk));

    if (pThunks == NULL)
        justQueryCount = TRUE;

    if (!justQueryCount)
    {
        size = 15;
        thunks = HkpAlloc(size * sizeof(DWORD));
    }

    while (thunk->u1.AddressOfData != 0)
    {
        if (!justQueryCount)
        {
            if (size == count)
            {
                size += 10;
                thunks = HkpRealloc(thunks, size * sizeof(DWORD));
            }

            thunks[count++] = thunk->u1.AddressOfData;
        }
        else
            count++;

        thunk++;
    }


    if (!count)
    {
        if (!justQueryCount)
            HkpFree(thunks);
        return FALSE;
    }


    if (pThunks)
        *pThunks = thunks;
    
    *pCount = count;

    return TRUE;
}

ULONG HkpQueryTotalThunkCount(PIMAGE_IMPORT_DESCRIPTOR pDesc)
{
    ULONG total = 0, count = 0;

    PIMAGE_IMPORT_DESCRIPTOR desc = pDesc;

    while (desc->FirstThunk != 0)
    {
        HkpGetThunksFromImportDescriptor(desc, NULL, &count);
        total += count;

        desc++;
    }

    return total;
}

BOOL HkInjectModule(PCHAR moduleName, PCHAR functionName, USHORT order)
{
    PBYTE pData,pModuleName,pHint,pFunctionName,pOFTData = NULL;
    PDWORD pIatEntry,pOFT=NULL;
    ULONG injectDataSize;
    DWORD newImportDirectoryRVA, newVirtSize, newRawSize, oftBlockSize = 0;
    BOOL buildOFT=FALSE;

    PIMAGE_IMPORT_DESCRIPTOR pImpDesc = NULL, injectDesc = NULL;

    PIMAGE_SECTION_HEADER pLastSection = HkpGetLastSection();

    if (HkpImports->OriginalFirstThunk == NULL)
    {
        if (HkpBuildOFT)
            buildOFT = TRUE;
    }

    //                              +1 for our import, +1 for NULL term
    injectDataSize = (HkpImportModuleCount + 1 + 1) * sizeof(IMAGE_IMPORT_DESCRIPTOR);


    injectDataSize += sizeof(DWORD); //firstthunk value
    injectDataSize += strlen(moduleName) + 1;
    injectDataSize += strlen(functionName) + 1;
    injectDataSize += sizeof(WORD); //sizeof(WORD) for hint



    if (buildOFT) //append OFTs totalthunks + 1 null term for each import dir, +2 for injection OFT and its null term
    {
        oftBlockSize = (HkpQueryTotalThunkCount(HkpImports) + HkpImportModuleCount) + 2;
        oftBlockSize *= sizeof(DWORD);
        injectDataSize += oftBlockSize;
    }

    injectDataSize = ALIGNTO(injectDataSize, HkpNtHeader->OptionalHeader.FileAlignment);

    xwr("required data size for injection is %lu bytes", injectDataSize);

    pData = HkpAlloc(injectDataSize);

    
    if (!pData)
        return FALSE;

    
    pImpDesc = (PIMAGE_IMPORT_DESCRIPTOR)pData;

    if (buildOFT)
    {
        pOFTData = pData + ((HkpImportModuleCount + 1 + 1) * sizeof(IMAGE_IMPORT_DESCRIPTOR));
        pOFT = (PDWORD)pOFTData;

        pModuleName = ((PBYTE)pOFT) + oftBlockSize;
    }
    else
        pModuleName = pData + ((HkpImportModuleCount + 1 + 1) * sizeof(IMAGE_IMPORT_DESCRIPTOR));

    pHint = pModuleName + strlen(moduleName) + 1;
    pFunctionName = pHint + 2;
    pIatEntry = pFunctionName + strlen(functionName) + 1;

    for (LONG i = 0, j=0; i < HkpImportModuleCount; i++,j++)
    {
        if (injectDesc == NULL && i == order)
        {
            injectDesc = &pImpDesc[i];
            i--;
        }
        else
        {
            memcpy(&pImpDesc[j], &HkpImports[i], sizeof(IMAGE_IMPORT_DESCRIPTOR));

            if (buildOFT)
            {
                PIMAGE_THUNK_DATA thunk = (PIMAGE_THUNK_DATA)(HkpImageBase + RVA2OFF(pImpDesc[j].FirstThunk));
                
                pImpDesc[j].OriginalFirstThunk = HkpLocalAddressToSectionAppendingRVA((DWORD)pData, pOFT, pLastSection);

                while (thunk->u1.AddressOfData != 0)
                {
                    *pOFT = thunk->u1.AddressOfData;
                    pOFT++;

                    thunk++;
                }

                //Let one block for NULL TERM
                pOFT++;
            }

        }
    }

    if (!injectDesc)
    {
        injectDesc = &pImpDesc[HkpImportModuleCount];
    }

    
    injectDesc->ForwarderChain = 0;
    injectDesc->TimeDateStamp = 0;
    injectDesc->Name = HkpLocalAddressToSectionAppendingRVA((DWORD)pData, pModuleName, pLastSection);
    injectDesc->FirstThunk = HkpLocalAddressToSectionAppendingRVA((DWORD)pData, pIatEntry, pLastSection);

    *pIatEntry = HkpLocalAddressToSectionAppendingRVA((DWORD)pData, pHint, pLastSection);

    if (buildOFT)
    {
        *pOFT = *pIatEntry;
        injectDesc->OriginalFirstThunk = HkpLocalAddressToSectionAppendingRVA((DWORD)pData, pOFT, pLastSection);
    }

    strcpy(pModuleName, moduleName);
    strcpy(pFunctionName, functionName);
    (*(PWORD)pHint) = 0;

    newImportDirectoryRVA = HkpLocalAddressToSectionAppendingRVA((DWORD)pData, pImpDesc, pLastSection);

    newVirtSize = pLastSection->Misc.VirtualSize + injectDataSize;
    newRawSize = pLastSection->SizeOfRawData + injectDataSize;

    HkUnmapPE();

    return HkpWriteToPE(newImportDirectoryRVA, pData, injectDataSize,newVirtSize,newRawSize);
}

void PrintBanner()
{
    wr("The hooker\n\na tool for dll injection\nOguz Kartal Jan. 2019\n\n\n");
}

void PrintUsage()
{
    wr("hooker [EXECUTABLE_PATH] [MODULE_FILENAME] [FUNCTION] {OPTIONS}\n");
    wr("\n{OPTIONS}:\n");
    wr("\t-o[INT_VALUE] - Module injection order. can be between 0 and count of the imports\n");
    wr("\t\tif specified value greater than import count the injector will be append at the end.\n\n");
    wr("\t-oft - If the injector detects that the PE wasn't built with OFT (OriginalFirstThunk)\n");
    wr("\t\t(usually very old compilers does not build OFTs), the injector will be building OFT\n");
    wr("\t\tto make easy hooking at the runtime.");

}

BOOL HandleArgs(int argc, char **argv)
{
    SHORT order = 0;
    PCHAR pOption = NULL;
    USHORT optLen;

    PrintBanner();

    if (argc < 4)
    {
        PrintUsage();
        return FALSE;
    }

    strcpy(HkpImagePath, argv[1]);

    if (argc >= 5)
    {
        for (INT i = 4; i < argc; i++)
        {
            pOption = argv[i];
            optLen = strlen(pOption);

            if (!strcmp(pOption, "-oft"))
                HkpBuildOFT = TRUE;
            else if (!strncmp(pOption, "-o", 2))
            {
                if (optLen > 2)
                    HkpInjectOrder = atoi(pOption + 2);
                else
                {
                    PrintUsage();
                    xwr("\n\nMissing argument usage. There is no -o value supplied\n");
                    return FALSE;
                }
            }
        }
    }

    return TRUE;
}

int main(int argc, char **argv)
{

    if (!HandleArgs(argc, argv))
        return 1;

    xwr("mapping PE into memory");

    if (!HkLoadPE(HkpImagePath))
    {
        xwr("pe file could not be loaded\n");
        return 1;
    }

    if (HkpInjectOrder == -1)
        HkpInjectOrder = HkpImportModuleCount;

    if (HkInjectModule(argv[2], argv[3], HkpInjectOrder))
        xwr("job done\n");
    else
        xwr("injection failed\n");

    HkUnloadPE();
    
    return 0;
}

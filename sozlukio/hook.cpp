#include "stdafx.h"

#include <stdio.h>
#include <stdlib.h>
#include "helper.h"

#if 0
#define HKLOG(x,...) DLOG(x,__VA_ARGS__)
#else
#define HKLOG(x,...)
#endif

typedef struct __HOOK_FUNCTION
{
    CHAR        moduleName[64];
    CHAR        name[64];
    DWORD_PTR   pointer;
    DWORD_PTR   original;
    DWORD_PTR   hook;
}HOOK_FUNCTION,*PHOOK_FUNCTION;

typedef struct __HOOK_LIST
{
    PHOOK_FUNCTION Hooks;
    ULONG HookListSize;
    ULONG HookCount;
}HOOK_LIST,*PHOOK_LIST;

HMODULE HkpExe = NULL;
DWORD HkpImageBase = 0;

PIMAGE_IMPORT_DESCRIPTOR HkpImports = NULL;
extern HMODULE HkpMyselfModule;

#define VA2OFF(va) ((va)-HkpImageBase)


HOOK_LIST HkpHooks = { 0 };
ULONG HkpAllocCount = 0;

PBYTE HkpCalloc(ULONG count, ULONG itemSize)
{
    PBYTE mem = (PBYTE)calloc(count, itemSize);

    if (!mem)
        return NULL;

    InterlockedIncrement((volatile ULONG *)&HkpAllocCount);
    memset(mem, 0, count * itemSize);



    return mem;
}

PVOID HkpReAlloc(PVOID old, ULONG oldSize, ULONG amount)
{
    PBYTE mem = (PBYTE)realloc(old, oldSize + amount);

    if (mem)
    {
        if (oldSize > 0)
            RtlZeroMemory(mem + oldSize, amount);
        
        return mem;
    }

    return NULL;
}

PVOID HkpAlloc(ULONG size)
{
    return HkpCalloc(size, 1);
}


void HkpFree(PVOID mem)
{
    free(mem);

    InterlockedDecrement((volatile ULONG *)&HkpAllocCount);
}

BOOL HkpInitHookList(ULONG initialSize)
{
    RtlZeroMemory(&HkpHooks, sizeof(HOOK_LIST));

    HkpHooks.Hooks = (PHOOK_FUNCTION)HkpCalloc(initialSize, sizeof(HOOK_FUNCTION));

    if (!HkpHooks.Hooks)
        return FALSE;

    HkpHooks.HookListSize = initialSize;

    return TRUE;
}

//These functions are not thread safe. If you need to work these as thread safe
//you have to add additional synchronization primitives in it.

BOOL HkpExtendList(ULONG howMany)
{
    PVOID pNew;

    pNew = HkpReAlloc(HkpHooks.Hooks, HkpHooks.HookListSize * sizeof(HOOK_FUNCTION), howMany * sizeof(HOOK_FUNCTION));

    if (!pNew)
        return FALSE;

    HkpHooks.Hooks = (PHOOK_FUNCTION)pNew;
    HkpHooks.HookListSize += howMany;

    return TRUE;
}

#define GET_STRING(x) ((PCHAR)(HkpImageBase + (x)))

USHORT HkpFindModules(LPCSTR moduleName, PIMAGE_IMPORT_DESCRIPTOR **pDescs)
{
    USHORT foundCount = 0;

    *pDescs = (PIMAGE_IMPORT_DESCRIPTOR *)HkpCalloc(20, sizeof(PIMAGE_IMPORT_DESCRIPTOR));

    PIMAGE_IMPORT_DESCRIPTOR idesc = HkpImports;

    while (idesc->OriginalFirstThunk != NULL || idesc->FirstThunk != NULL)
    {
        if (!_stricmp(GET_STRING(idesc->Name), moduleName))
        {
            (*pDescs)[foundCount++] = idesc;
        }

        idesc++;
    }

    if (!foundCount)
    {
        HkpFree(*pDescs);
        *pDescs = NULL;
        return 0;
    }

    return foundCount;
}

DWORD_PTR HkpFindFunctionAddress(PIMAGE_IMPORT_DESCRIPTOR desc, LPCSTR functionName)
{
    PIMAGE_THUNK_DATA realThunk,thunk;
    PIMAGE_IMPORT_BY_NAME impName;

    thunk = (PIMAGE_THUNK_DATA)(HkpImageBase + desc->OriginalFirstThunk);
    realThunk = (PIMAGE_THUNK_DATA)(HkpImageBase + desc->FirstThunk);

    //TODO: WE WONT EXPECT THIS
    if (desc->OriginalFirstThunk == NULL)
        return NULL;

    while (thunk->u1.AddressOfData != 0)
    {
        if (!(thunk->u1.Ordinal & 0x80000000))
        {
            impName = (PIMAGE_IMPORT_BY_NAME)(HkpImageBase + thunk->u1.Function);

            if (!_stricmp((PCHAR)impName->Name, functionName))
            {
                return (DWORD_PTR)&(realThunk->u1.Function);
            }
        }

        realThunk++;
        thunk++;
    }

    return NULL;
}

BOOL HkpRegisterHook(const PCHAR moduleName, const PCHAR fnName, DWORD_PTR hookFn, DWORD_PTR origFn, DWORD impPtr)
{
    PHOOK_FUNCTION pHook;
    INT hookId = HkpHooks.HookCount;

    if (HkpHooks.HookCount == HkpHooks.HookListSize)
    {
        if (!HkpExtendList(10))
            return FALSE;
    }

    pHook = &HkpHooks.Hooks[hookId];

    pHook->hook = hookFn;
    pHook->original = origFn;

    strcpy_s(pHook->moduleName, sizeof(pHook->moduleName), moduleName);
    strcpy_s(pHook->name, sizeof(pHook->name), fnName);

    pHook->pointer = impPtr;

    HkpHooks.HookCount++;

    return TRUE;
}

void HkpDeregister(INT hookId)
{
    LONG rem;

    if (hookId != HkpHooks.HookCount - 1)
    {
        rem = HkpHooks.HookCount - (hookId + 1);
        memmove(HkpHooks.Hooks + hookId, HkpHooks.Hooks + hookId + 1, rem * sizeof(HOOK_FUNCTION));
    }

    HkpHooks.HookCount--;

    memset(HkpHooks.Hooks + HkpHooks.HookCount, 0, sizeof(HOOK_FUNCTION));
}

BOOL HkpUnhook(PHOOK_FUNCTION pHook, INT hookId)
{
    BOOL status = FALSE;

    if (InterlockedCompareExchange((volatile ULONG *)pHook->pointer, pHook->original, pHook->hook)
        == pHook->hook)
    {
        if (hookId > 0)
            HkpDeregister(hookId);
        
        status = TRUE;
    }

    return status;
}

BOOL HkInitHook()
{
    PBYTE pImage;
    PIMAGE_DOS_HEADER doshdr;
    PIMAGE_NT_HEADERS nthdr;
    PIMAGE_DATA_DIRECTORY importDir;

    HkpExe = GetModuleHandleA(NULL);
    pImage = (PBYTE)HkpExe;

    HkpImageBase = (DWORD)pImage;

    doshdr = (PIMAGE_DOS_HEADER)pImage;


    nthdr = (PIMAGE_NT_HEADERS)(pImage + doshdr->e_lfanew);

    importDir = &nthdr->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];

    HkpImports = (PIMAGE_IMPORT_DESCRIPTOR)(HkpImageBase + importDir->VirtualAddress);

    HKLOG("Executable base: %p, Imports: %p", HkpImageBase, HkpImports);

    if (!HkpInitHookList(10))
        return FALSE;



    return TRUE;
}

void HkDestroyHooks()
{
    PHOOK_FUNCTION pHook = HkpHooks.Hooks;
    LONG hookCount = HkpHooks.HookCount;

    while (hookCount--)
    {
        HkpUnhook(pHook, -1);
        pHook++;
    }

    HkpFree(HkpHooks.Hooks);
}

DWORD_PTR HkHookAPI(LPCSTR moduleName, LPCSTR functionName, DWORD_PTR hookFunction)
{
    USHORT moduleCount = 0;
    PIMAGE_IMPORT_DESCRIPTOR *pDesc = NULL;
    DWORD_PTR origAddr = NULL, origFunc=NULL,old = NULL;

    moduleCount = HkpFindModules(moduleName, &pDesc);

    HKLOG("%d modules found", moduleCount);

    for (LONG i = 0; i < moduleCount; i++)
    {
        if (pDesc[i]->OriginalFirstThunk == NULL)
        {
            //TODO: Implement
            HKLOG("OFT not available for %x. Need to map static exec file to find imports by name\n",&pDesc[i]);
            continue;
        }

        origAddr = HkpFindFunctionAddress(pDesc[i], functionName);

        if (origAddr)
        {
            origFunc = *((DWORD *)origAddr);
            
            HKLOG("Original API: %p (%p), Hook Func: %p", origAddr,origFunc, hookFunction);

            old = (DWORD_PTR)InterlockedExchange((volatile ULONG *)origAddr, hookFunction);

            HkpRegisterHook((const PCHAR)moduleName, (const PCHAR)functionName, hookFunction, origFunc, origAddr);
        }
        
    }

    if (pDesc)
        HkpFree(pDesc);

    return old;
}


BOOL HkUnhookAPI(DWORD_PTR hookFunction)
{
    PHOOK_FUNCTION pHook = NULL;
    INT id;

    for (id = 0; id < HkpHooks.HookCount; id++)
    {
        if (HkpHooks.Hooks[id].hook == hookFunction)
        {
            pHook = &HkpHooks.Hooks[id];
            break;
        }
    }

    if (!pHook)
        return FALSE;


    return HkpUnhook(pHook,id);
}
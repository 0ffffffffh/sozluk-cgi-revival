#include "stdafx.h"
#include "memtrck.h"
#include <stdlib.h>
#include "helper.h"



#define MTLOG(s,...) DLOG(s,__VA_ARGS__)

/*
Doubly Linked list implementation.

Windows API set provides linked list implementation both singly and doubly list
but the doubly list impl. provided to kernel mode only.

That's totally meaningless, so I had to implement my own Doubly list.

The list insertion and deletion operations are thread safe which is
protected by spinlock.
*/
typedef struct __LIST_ENTRY2
{
    struct __LIST_ENTRY2 *Flink;
    struct __LIST_ENTRY2 *Blink;
}LIST_ENTRY2,*PLIST_ENTRY2;

typedef struct __LIST_ENTRY2_HEADER
{
    LIST_ENTRY2     Head;
    ULONG           Count;
    volatile ULONG  Lock;
}LIST_ENTRY2_HEADER,*PLIST_ENTRY2_HEADER;


typedef struct __MEMORY_HEADER
{
    DWORD       Magic;
    LIST_ENTRY2 Entry;
    CHAR        CallerName[64];
    CHAR        FileName[MAX_PATH];
    INT         LineNumber;
    ULONG       Size;
    UCHAR       Memory[0];
}MEMORY_HEADER, *PMEMORY_HEADER;


LIST_ENTRY2_HEADER MtpTrackList;

void MtpLockList(PLIST_ENTRY2_HEADER listHead)
{
    while (InterlockedCompareExchange(&listHead->Lock, 1, 0) == 1)
        _mm_pause();
}

void MtpUnlockList(PLIST_ENTRY2_HEADER listHead)
{
    InterlockedCompareExchange(&listHead->Lock, 0, 1);
}

void MtpInitializeListHead(PLIST_ENTRY2_HEADER listHead)
{
    RtlZeroMemory(listHead, sizeof(LIST_ENTRY2_HEADER));
    listHead->Head.Flink = listHead->Head.Blink = &listHead->Head;
}


BOOL MtpInsertListEntryTail(PLIST_ENTRY2_HEADER listHead, PLIST_ENTRY2 entry)
{
    PLIST_ENTRY2 oldTail;

    MtpLockList(listHead);

    oldTail = listHead->Head.Blink;

    entry->Flink = oldTail->Flink;
    entry->Blink = oldTail;

    oldTail->Flink = entry;
    listHead->Head.Blink = entry;

    listHead->Count++;

    MtpUnlockList(listHead);

    return TRUE;
}

BOOL MtpRemoveListEntry(PLIST_ENTRY2_HEADER listHead, PLIST_ENTRY2 entry)
{
    BOOL status;

    MtpLockList(listHead);

    if (!listHead->Count)
    {
        status = FALSE;
        goto unlockExit;
    }

    entry->Blink->Flink = entry->Flink;
    entry->Flink->Blink = entry->Blink;
    
    listHead->Count--;

    status = TRUE;

unlockExit:

    MtpUnlockList(listHead);

    return status;
}

BOOL MtInitialize()
{
    MtpInitializeListHead(&MtpTrackList);
    return TRUE;
}

#define ADDR2MEMINFO(addr) ( ((ULONG_PTR)(addr)) - sizeof(MEMORY_HEADER) )

PMEMORY_HEADER MtProbeMemoryInfo(PVOID addr)
{
    PLIST_ENTRY2 pEntry, pAddrEntry;

    pEntry = MtpTrackList.Head.Flink;

    pAddrEntry = (PLIST_ENTRY2)(ADDR2MEMINFO(addr) + sizeof(DWORD));

    for (INT i = 0; i < MtpTrackList.Count; i++)
    {
        if (pEntry == pAddrEntry)
        {
            return (PMEMORY_HEADER)ADDR2MEMINFO(addr);
        }

        pEntry = pEntry->Flink;
    }

    return NULL;
}

BOOL MtpInsertTracklist(PMEMORY_HEADER pMem, ULONG size, const PCHAR caller, const PCHAR fileName, const INT line)
{
    pMem->Magic = 0xFE1FE100;
    pMem->Size = size;

    pMem->LineNumber = line;
    strcpy(pMem->CallerName, caller);
    strcpy(pMem->FileName, fileName);

    return MtpInsertListEntryTail(&MtpTrackList, &pMem->Entry);
}

PVOID MtReallocTrackable(PVOID memory, ULONG amount)
{
    ULONG oldSize, oldSizeWithOverhead;
    PBYTE newMem;
    PMEMORY_HEADER memInfo;

    memInfo = MtProbeMemoryInfo(memory);

    if (!memInfo)
        return NULL;

    //we have to detach the memory info entry from the list first.
    //memory re allocation may move the original memory pointer
    //to the different place if requested size does not fit its expandable area
    //so, the list entry pointer becomes invalid.

    MtpRemoveListEntry(&MtpTrackList, &memInfo->Entry);

    oldSize = memInfo->Size;
    oldSizeWithOverhead = oldSize + sizeof(MEMORY_HEADER);

    newMem = (PBYTE)realloc(memInfo, oldSizeWithOverhead + amount);

    if (!newMem)
    {
        MTLOG("re-alloc failed. %lu bytes", oldSizeWithOverhead + amount);
        return NULL;
    }

    memInfo = (PMEMORY_HEADER)newMem;
    memInfo->Size = oldSize + amount;

    newMem = (PBYTE)memInfo->Memory;

    //clear possible junk data from the extended memory range 
    memset(newMem + oldSize, 0, amount);

    MtpInsertListEntryTail(&MtpTrackList, &memInfo->Entry);
    
    return newMem;
}

PVOID MtAllocateTrackable(ULONG size, const PCHAR caller, const PCHAR fileName, const INT line)
{
    ULONG realSize;
    PMEMORY_HEADER pMem;

    realSize = sizeof(MEMORY_HEADER) + size;

    pMem = (PMEMORY_HEADER)malloc(realSize);

    if (!pMem)
    {
        MTLOG("allocation failed. %lu bytes", realSize);
        return NULL;
    }

    memset(pMem, 0, realSize);

    MtpInsertTracklist(pMem, size, caller, fileName, line);

    return pMem->Memory;
}

void MtFreeTrackable(PVOID memory)
{
    PMEMORY_HEADER memInfo;

    memInfo = MtProbeMemoryInfo(memory);

    if (memInfo)
    {
        MtpRemoveListEntry(&MtpTrackList, &memInfo->Entry);
        free(memInfo);
    }
}

void MtReportLeaks()
{
    PLIST_ENTRY2 pEntry;
    PMEMORY_HEADER pMem;
    ULONG count;
    CHAR file[MAX_PATH];

    MtpLockList(&MtpTrackList);

    count = MtpTrackList.Count;
    pEntry = MtpTrackList.Head.Flink;

    while (count--)
    {
        pMem = (PMEMORY_HEADER)CONTAINING_RECORD(pEntry, MEMORY_HEADER, Entry);

        memset(file, 0, sizeof(file));
        HlpGetFileNameFromPath(pMem->FileName, file, sizeof(file));

        DLOG("memsize: %lu, caller: %s, file: %s, line: %d",
            pMem->Size, pMem->CallerName, file, pMem->LineNumber);

    }


    MtpUnlockList(&MtpTrackList);
}
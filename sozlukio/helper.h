#pragma once

#include "stdafx.h"
#include "stypes.h"

/*
Only enabled on debug builds.
*/

#if _DEBUG
#define TRACK_ALLOC 1
#endif

#if defined(TRACK_ALLOC)
#include "memtrck.h"
#endif


typedef struct __AUTO_BUFFER
{
    PBYTE buffer;
    ULONG size;
    ULONG pos;
    BOOL isDynamic;
}AUTO_BUFFER, *PAUTO_BUFFER;


typedef struct __DYNAMIC_ARRAY_OF_TYPE
{
    PBYTE data;
    PBYTE *container;
    ULONG sizeOfType;
    ULONG arraySize;
    ULONG index;
}DYNAMIC_ARRAY_OF_TYPE,*PDYNAMIC_ARRAY_OF_TYPE;

typedef struct __TOKENIZE_CONTEXT
{
    PCHAR data;
    PCHAR delim;
    CHAR saveChr;
    ULONG pos;
    ULONG flag;
}TOKENIZE_CONTEXT,*PTOKENIZE_CONTEXT;

#define TOKF_SKIP_EMPTY 1
#define TOKF_DONE 2

#define RL_INITIAL 1
#define RL_AFTER_DECODE 2
#define RL_AFTER_ENCODE 3

typedef void(*KEYVALUE_REENCODE_CALLBACK)(PREQUEST_KEYVALUE pKv, UCHAR level);

#ifndef TRACK_ALLOC

PVOID HlpAlloc(ULONG size);
VOID HlpFree(PVOID mem);
PVOID HlpReAlloc(PVOID mem, ULONG oldSize, ULONG amount);
PVOID HlpReAlloc2(PVOID mem, ULONG newSize);
#else

#define HlpAlloc(s) MtAlloc(s)
#define HlpFree(m) MtFree(m)
#define HlpReAlloc(mem, old, amount) MtReAlloc(mem, amount)
#define HlpReAlloc2(mem, newsize) MtReAlloc(mem,newsize)


#endif

#if _DEBUG
#define HlpDbgPrint(v,...) HlpxDbgPrint(v,__VA_ARGS__)
#else
#define HlpDbgPrint(v,...)
#endif

#if 0
#define DLOG(x,...) HlpDbgPrint("(%s : %d) => " x,__FUNCTION__,__LINE__,__VA_ARGS__)
#else
#define DLOG(x,...) HlpSendLogString("(%s : %d) => " x,__FUNCTION__,__LINE__,__VA_ARGS__)
#endif

#if _DEBUG
#define VERBOSE(x,...) DLOG("Verbose: " x, __VA_ARGS__)
#else
#define VERBOSE(x,...)
#endif

#define DASSERT(expr) if (!(expr)) DLOG("Assertation failed. %s",#expr)


void HlpxDbgPrint(LPCSTR format, ...);

void HlpInitLogClient();

void HlpUninitLogClient();

void HlpSendLogString(LPCSTR format, ...);

ULONG HlpRemoveString(PCHAR str, ULONG length, PCHAR findStr);

LONG HlpStrPos(PCHAR str, PCHAR substr);

PCHAR HlpTokenize(PCHAR str, PCHAR delim, ULONG flag, PTOKENIZE_CONTEXT tokCtx);

BOOL HlpInitDynamicArray(PBYTE *container, ULONG sizeOfType, ULONG initialSize, PDYNAMIC_ARRAY_OF_TYPE arrPtr);

BOOL HlpExtendDynamicArray(PDYNAMIC_ARRAY_OF_TYPE arrPtr, ULONG amount);

#define HlpNeedsExtend(arrPtr) ((arrPtr)->index == (arrPtr)->arraySize)

VOID HlpFreeDynamicArray(PDYNAMIC_ARRAY_OF_TYPE arrPtr);

BOOL HlpInitializeAutoBuffer(PAUTO_BUFFER pBuffer, ULONG initialSize);

PAUTO_BUFFER HlpCreateAutoBuffer(ULONG initialSize);

void HlpDisposeAutoBuffer(PAUTO_BUFFER pBuffer);

PBYTE HlpTakeBufferOwnershipAndDestroyBufferObject(PAUTO_BUFFER buffer);

BOOL HlpWriteIntoAutoBuffer(PAUTO_BUFFER pBuffer, PVOID data, ULONG size);

DWORD HlpReadFromNativeFileHandle(PAUTO_BUFFER pBuffer, HANDLE handle, ULONG readSize);

BOOL HlpInsertIntoAutoBuffer(PAUTO_BUFFER pBuffer, ULONG index, PVOID data, ULONG size, ULONG overwriteSize);


DWORD HlpUrlEncodeAscii(PCHAR value, ULONG length, PCHAR *encodedValue, BOOL includeReserved);

DWORD HlpUrlDecodeAsAscii(PCHAR value, ULONG length);

DWORD HlpReEncodeAsAscii(PCHAR value, ULONG length, PCHAR *encodedValue, KEYVALUE_REENCODE_CALLBACK cb);

//if fileNameBuf set NULL the filename will be located in path.
BOOL HlpGetFileNameFromPath(PCHAR path, PCHAR fileNameBuf, ULONG bufSize);

BOOL HlpGetExecutableName(PCHAR exeNameBuf, ULONG bufSize);

BOOL HlpBuildEntryContextFromWriteData(PAUTO_BUFFER dataBuffer, PSOZLUK_ENTRY_CONTEXT sec);

#define HlpHangUntilDebuggerAttach(hint) HlpxHangUntilDebuggerAttach(__FUNCTION__,hint)

BOOL HlpxHangUntilDebuggerAttach(LPCSTR func, LPCSTR hint);
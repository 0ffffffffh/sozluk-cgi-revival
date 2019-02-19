#pragma once

#include "stdafx.h"

//Function Pointer Types
typedef HANDLE(WINAPI *pfnCreateFileA)(
    __in     LPCSTR lpFileName,
    __in     DWORD dwDesiredAccess,
    __in     DWORD dwShareMode,
    __in_opt LPSECURITY_ATTRIBUTES lpSecurityAttributes,
    __in     DWORD dwCreationDisposition,
    __in     DWORD dwFlagsAndAttributes,
    __in_opt HANDLE hTemplateFile
);

typedef BOOL(WINAPI *pfnReadFile)(
    __in        HANDLE hFile,
    __out_bcount_part_opt(nNumberOfBytesToRead, *lpNumberOfBytesRead) __out_data_source(FILE) LPVOID lpBuffer,
    __in        DWORD nNumberOfBytesToRead,
    __out_opt   LPDWORD lpNumberOfBytesRead,
    __inout_opt LPOVERLAPPED lpOverlapped
);

typedef BOOL (WINAPI *pfnWriteFile)(
    __in        HANDLE hFile,
    __in_bcount_opt(nNumberOfBytesToWrite) LPCVOID lpBuffer,
    __in        DWORD nNumberOfBytesToWrite,
    __out_opt   LPDWORD lpNumberOfBytesWritten,
    __inout_opt LPOVERLAPPED lpOverlapped
);

typedef BOOL(WINAPI *pfnCloseHandle)(
    __in        HANDLE hObject
);

typedef DWORD(WINAPI *pfnSetFilePointer)(
    __in        HANDLE hFile,
    __in        LONG lDistanceToMove,
    __inout_opt PLONG lpDistanceToMoveHigh,
    __in        DWORD dwMoveMethod
);

typedef DWORD (WINAPI *pfnGetFileSize)(
    HANDLE  hFile,
    LPDWORD lpFileSizeHigh
);

typedef DWORD(WINAPI *pfnGetEnvironmentVariableA)(
    __in_opt LPCSTR lpName,
    __out_ecount_part_opt(nSize, return +1) LPSTR lpBuffer,
    __in DWORD nSize
);

//Hook Function declarations

HANDLE WINAPI Hook_CreateFileA(
    __in     LPCSTR lpFileName,
    __in     DWORD dwDesiredAccess,
    __in     DWORD dwShareMode,
    __in_opt LPSECURITY_ATTRIBUTES lpSecurityAttributes,
    __in     DWORD dwCreationDisposition,
    __in     DWORD dwFlagsAndAttributes,
    __in_opt HANDLE hTemplateFile
);

BOOL WINAPI Hook_ReadFile(
    __in        HANDLE hFile,
    __out_bcount_part_opt(nNumberOfBytesToRead, *lpNumberOfBytesRead) __out_data_source(FILE) LPVOID lpBuffer,
    __in        DWORD nNumberOfBytesToRead,
    __out_opt   LPDWORD lpNumberOfBytesRead,
    __inout_opt LPOVERLAPPED lpOverlapped
);

BOOL WINAPI Hook_WriteFile(
    __in        HANDLE hFile,
    __in_bcount_opt(nNumberOfBytesToWrite) LPCVOID lpBuffer,
    __in        DWORD nNumberOfBytesToWrite,
    __out_opt   LPDWORD lpNumberOfBytesWritten,
    __inout_opt LPOVERLAPPED lpOverlapped
);

BOOL WINAPI Hook_CloseHandle(
    __in        HANDLE hObject
);

DWORD WINAPI Hook_SetFilePointer(
    __in        HANDLE hFile,
    __in        LONG lDistanceToMove,
    __inout_opt PLONG lpDistanceToMoveHigh,
    __in        DWORD dwMoveMethod
);


DWORD WINAPI Hook_GetFileSize(
    __in        HANDLE  hFile,
    __out_opt   LPDWORD lpFileSizeHigh
);


DWORD WINAPI Hook_GetEnvironmentVariableA(
    __in_opt LPCSTR lpName,
    __out_ecount_part_opt(nSize, return +1) LPSTR lpBuffer,
    __in DWORD nSize
);
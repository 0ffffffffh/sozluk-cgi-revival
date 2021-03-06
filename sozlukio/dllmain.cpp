// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "sozlukio.h"
#include "helper.h"
#include "RequestBridge.h"

CHAR SioExeName[MAX_PATH];
HMODULE HkpMyselfModule = NULL;
extern BOOL HkInitHook();
extern void HkDestroyHooks();

extern DWORD_PTR HkHookAPI(LPCSTR moduleName, LPCSTR functionName, DWORD_PTR hookFunction);
extern void SiopReleaseGlobalResources();

extern ULONG HkpAllocCount;

#ifndef TRACK_ALLOC
#define MtInitialize()
#define MtReportLeaks()
#endif

pfnCreateFileA _CreateFileA = NULL;
pfnReadFile _ReadFile = NULL;
pfnWriteFile _WriteFile = NULL;
pfnCloseHandle _CloseHandle = NULL;
pfnSetFilePointer _SetFilePointer = NULL;
pfnGetFileSize _GetFileSize = NULL;
pfnGetEnvironmentVariableA _GetEnvironmentVariableA = NULL;


BOOL SetHooks()
{
    if (!HkInitHook())
        return FALSE;

    _CreateFileA = (pfnCreateFileA)HkHookAPI("kernel32.dll", "CreateFileA", (DWORD_PTR)Hook_CreateFileA);
    _ReadFile = (pfnReadFile)HkHookAPI("kernel32.dll", "ReadFile", (DWORD_PTR)Hook_ReadFile);
    _WriteFile = (pfnWriteFile)HkHookAPI("kernel32.dll", "WriteFile", (DWORD_PTR)Hook_WriteFile);
    _CloseHandle = (pfnCloseHandle)HkHookAPI("kernel32.dll", "CloseHandle", (DWORD_PTR)Hook_CloseHandle);
    _SetFilePointer = (pfnSetFilePointer)HkHookAPI("kernel32.dll", "SetFilePointer", (DWORD_PTR)Hook_SetFilePointer);
    _GetFileSize = (pfnGetFileSize)HkHookAPI("kernel32.dll", "GetFileSize", (DWORD_PTR)Hook_GetFileSize);
    _GetEnvironmentVariableA = (pfnGetEnvironmentVariableA)HkHookAPI("kernel32.dll", "GetEnvironmentVariableA", (DWORD_PTR)Hook_GetEnvironmentVariableA);

    return TRUE;
}

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        HkpMyselfModule = hModule;
        MtInitialize();

        if (!RbConnectBridge())
        {
            DLOG("There is no backend!!! API hooks disabled.");
            return TRUE;
        }

        HlpInitLogClient();

        SetHooks();
        HlpGetExecutableName(SioExeName, sizeof(SioExeName));
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        SiopReleaseGlobalResources();
        HkDestroyHooks();
        MtReportLeaks();

        HlpUninitLogClient();
        
        break;
    }
    return TRUE;
}


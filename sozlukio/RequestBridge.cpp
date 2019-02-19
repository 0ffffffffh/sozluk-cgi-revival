#include "stdafx.h"
#include "RequestBridge.h"
#include "helper.h"
#include <stdlib.h>

HANDLE  RbpPipeHandle;



BOOL RbInitTransport(PBRIDGE_TRANSPORT pTransport, ULONG initSize)
{
    RtlZeroMemory(pTransport, sizeof(BRIDGE_TRANSPORT));

    if (HlpInitializeAutoBuffer(&pTransport->Buffer, initSize))
    {
        pTransport->Status |= BTF_ACTIVE;
        return TRUE;
    }

    return FALSE;
}

BOOL RbWriteBridgeTransport(PBRIDGE_TRANSPORT pTransport, PVOID data, ULONG dataLen)
{
    return HlpWriteIntoAutoBuffer(&pTransport->Buffer, data, dataLen);
}

void RbFreeTransport(PBRIDGE_TRANSPORT pTransport)
{
    HlpDisposeAutoBuffer(&pTransport->Buffer);
    memset(pTransport, 0, sizeof(BRIDGE_TRANSPORT));
}

void RbFlushTransport(PBRIDGE_TRANSPORT pTransport)
{
    memset(pTransport->Buffer.buffer, 0, pTransport->Buffer.pos);
    pTransport->Buffer.pos = 0;
}



BOOL RbConnectBridge()
{
    CHAR pipeName[MAX_PATH];

    strcpy(pipeName, "\\\\.\\pipe\\");

    strcat(pipeName, "sozluk_request_bridge_pipe");

    RbpPipeHandle = CreateFileA(
        pipeName,
        GENERIC_READ | GENERIC_WRITE,
        0,NULL,OPEN_EXISTING,0,NULL
    );

    if (RbpPipeHandle == INVALID_HANDLE_VALUE)
    {
        DLOG("Invalid handle value: Err: %x", GetLastError());
        RbpPipeHandle = NULL;
        return FALSE;
    }

    return TRUE;
}

void RbCloseBridge()
{
    DLOG("bridge closing");

    CloseHandle(RbpPipeHandle);
    RbpPipeHandle = NULL;
}

BOOL RbPassRequestToBackend(PBRIDGE_TRANSPORT pTransport)
{
    DWORD written = 0;

    if (!RbpPipeHandle)
        return FALSE;

    if (!WriteFile(RbpPipeHandle, &pTransport->Buffer.pos, sizeof(ULONG), &written, NULL))
        return FALSE;

    if (!WriteFile(RbpPipeHandle, pTransport->Buffer.buffer, pTransport->Buffer.pos, &written, NULL))
        return FALSE;

    return pTransport->Buffer.pos == written;
}

DWORD RbReadResponseFromBackend(PBRIDGE_TRANSPORT transport)
{
    DWORD readSize = 0;
    ULONG respSize;
    BOOL previouslyActive = FALSE;

    if (!RbpPipeHandle)
        return 0;

    if (!ReadFile(RbpPipeHandle, &respSize, sizeof(ULONG), &readSize, NULL))
        return 0;

    
    if (!(transport->Status & BTF_ACTIVE))
        RbInitTransport(transport, respSize + 1);
    else
        previouslyActive = TRUE;

    readSize = HlpReadFromNativeFileHandle(&transport->Buffer, RbpPipeHandle, respSize);

    if (!readSize)
    {
        if (previouslyActive)
            RbFlushTransport(transport);
        else
            RbFreeTransport(transport);
        
        return 0;
    }

    return readSize;
}



#pragma once

#include "helper.h"

#define BTF_ACTIVE 1

typedef struct __BRIDGE_TRANSPORT
{
    DWORD Status;
    AUTO_BUFFER Buffer;
}BRIDGE_TRANSPORT, *PBRIDGE_TRANSPORT;

BOOL RbInitTransport(PBRIDGE_TRANSPORT pTransport, ULONG initSize);

BOOL RbWriteBridgeTransport(PBRIDGE_TRANSPORT pTransport, PVOID data, ULONG dataLen);

void RbFreeTransport(PBRIDGE_TRANSPORT pTransport);

void RbFlushTransport(PBRIDGE_TRANSPORT pTransport);

BOOL RbConnectBridge();

void RbCloseBridge();

BOOL RbPassRequestToBackend(PBRIDGE_TRANSPORT pTransport);

DWORD RbReadResponseFromBackend(PBRIDGE_TRANSPORT transport);

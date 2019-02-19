#pragma once

#include "stdafx.h"


#define MtAlloc(size) MtAllocateTrackable(size,__FUNCTION__,__FILE__,__LINE__);
#define MtFree(mem) MtFreeTrackable(mem)
#define MtReAlloc(mem,amount) MtReallocTrackable(mem,amount)

BOOL MtInitialize();

PVOID MtReallocTrackable(PVOID memory, ULONG amount);

PVOID MtAllocateTrackable(ULONG size, const PCHAR caller, const PCHAR fileName, const INT line);
void MtFreeTrackable(PVOID memory);
void MtReportLeaks();
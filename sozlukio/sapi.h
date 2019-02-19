#pragma once

#include "stdafx.h"
#include "stypes.h"


BOOL SzAddEntry(PSOZLUK_ENTRY_CONTEXT entry);

BOOL SzGetSuserPassword(PCHAR suser, PCHAR buffer, ULONG bufSize);

BOOL SzAuthSuser(PCHAR suser, PCHAR password);

BOOL SzSearch(PCHAR index, PCHAR content, PCHAR suser, PCHAR beginDate, PCHAR endDate, PCHAR pagerHash, INT pageNum, PINDEX_QUERY_RESULT pResult);

BOOL SzGetEntriesByBaslik(PCHAR baslik, INT pageNumber, UINT baslikId, BOOL latest, PENTRY_VIEW_QUERY_RESULT pResult);


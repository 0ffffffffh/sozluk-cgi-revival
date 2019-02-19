#pragma once
#include "stdafx.h"

#define BASLIK_MARK_LEN 1
#define SUSER_MARK_LEN 1
#define NEW_LINE_LEN 2

#define MAX_BASLIK_LEN 50
#define MAX_SUSER_LEN 32
#define MAX_DATE_LEN 32
#define MAX_FIXED_DESC_LEN 64

#define MAX_BASLIK_SIZE (MAX_BASLIK_LEN + 1)
#define MAX_SUSER_SIZE (MAX_SUSER_LEN + 1)
#define MAX_DATE_SIZE (MAX_DATE_LEN + 1)
#define MAX_FIXED_DESC_SIZE (MAX_FIXED_DESC_LEN + 1)

typedef struct __SOZLUK_ENTRY_CONTEXT
{
    CHAR    Baslik[MAX_BASLIK_SIZE + BASLIK_MARK_LEN + NEW_LINE_LEN];
    CHAR    Suser[MAX_SUSER_SIZE + NEW_LINE_LEN];
    CHAR    Date[MAX_DATE_SIZE + NEW_LINE_LEN];
    CHAR    DescFixedBuf[MAX_FIXED_DESC_SIZE + NEW_LINE_LEN];
    PCHAR   Desc;
    ULONG   DescLength;
    ULONG   BaslikId;
    ULONG   RepCount;
}SOZLUK_ENTRY_CONTEXT, *PSOZLUK_ENTRY_CONTEXT;


typedef struct __ENTRY_VIEW_QUERY_RESULT
{
    CHAR Baslik[64];
    PSOZLUK_ENTRY_CONTEXT Entries;
    UINT BaslikId;
    UCHAR RecordsPerPage;
    ULONG AffectedRecordCount;
    ULONG TotalRecordCount;
    ULONG TotalPageCount;
    ULONG CurrentPageNumber;
    struct
    {
        DWORD State;
        DWORD RecordIndex;
    }Status;
}ENTRY_VIEW_QUERY_RESULT,*PENTRY_VIEW_QUERY_RESULT;

typedef struct __INDEX_QUERY_RESULT
{
    PSOZLUK_ENTRY_CONTEXT Entries;
    ULONG AffectedLogicalRecordCount;
    ULONG AffectedPhysicalRecordCount;
    ULONG TotalRecordCount;
    ULONG TotalPageCount;
    ULONG CurrentPageNumber;
    CHAR PagerHash[32 + 1];
    CHAR index[6];
    struct
    {
        DWORD State;
        DWORD RecordIndex;
    }Status;
}INDEX_QUERY_RESULT,*PINDEX_QUERY_RESULT;

typedef struct __REQUEST_KEYVALUE
{
    CHAR key[64];
    PCHAR value;
    ULONG valueLength;
}REQUEST_KEYVALUE, *PREQUEST_KEYVALUE;

typedef struct __SOZLUK_REQUEST
{
    REQUEST_KEYVALUE KvList[32];
    ULONG KvCount;
}SOZLUK_REQUEST, *PSOZLUK_REQUEST;

BOOL SioDisposeIndexQueryResult(PINDEX_QUERY_RESULT pqr, BOOL freeObject);

BOOL SioDisposeViewQueryResult(PENTRY_VIEW_QUERY_RESULT pvqr, BOOL freeObject);

BOOL SioFreeEntryContext(PSOZLUK_ENTRY_CONTEXT pec, BOOL freeObject);


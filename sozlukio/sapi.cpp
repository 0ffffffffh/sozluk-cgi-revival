#include "stdafx.h"
#include "sapi.h"
#include "helper.h"
#include "RequestBridge.h"
#include <stdlib.h>
#include <stdio.h>


#define SAPILOG(x,...) DLOG(x,__VA_ARGS__)

typedef struct __KEY_PROPERTY
{
    char Name[32];
    char Value[128];
}KEY_PROPERTY, *PKEY_PROPERTY;


typedef struct __RESPONSE_KEYVALUE
{
    CHAR            Key[64];
    PCHAR           Value;
    ULONG           ValueLength;
    PKEY_PROPERTY   Properties;
    ULONG           PropertyLength;
}RESPONSE_KEYVALUE,*PRESPONSE_KEYVALUE;

typedef struct __RESPONSE_CONTEXT
{
    PRESPONSE_KEYVALUE  List;
    ULONG               Size;
    ULONG               Length;
    LONG                MRI; //Multi read index
}RESPONSE_CONTEXT,*PRESPONSE_CONTEXT;

#define RT_ENTRY_ADD        0
#define RT_ENTRY_GET        1
#define RT_USER_GET_PASS    2
#define RT_SEARCH           3
#define RT_AUTH_SUSER       4

PCHAR SzpReqTypeString[]=
{
    "EntryAdd",
    "EntryGet",
    "UserGetPass",
    "Search",
    "AuthSuser"
};

#define SzpAlloc(size) HlpAlloc(size)
#define SzpReAlloc(old,olds,amount) HlpReAlloc(old,olds,amount)
#define SzpFree(mem) HlpFree(mem)

#define ENABLE_MVR(ctx) (ctx)->MRI=0
#define DISABLE_MVR(ctx) (ctx)->MRI=-1


BOOL SzpInitResponseContext(PRESPONSE_CONTEXT pResponse, ULONG initSize)
{
    pResponse->List = (PRESPONSE_KEYVALUE)SzpAlloc(initSize * sizeof(RESPONSE_KEYVALUE));

    if (!pResponse->List)
        return FALSE;

    pResponse->Size = initSize;
    pResponse->Length = 0;
    
    DISABLE_MVR(pResponse);

    return TRUE;
}

BOOL SzpExtendResponseContext(PRESPONSE_CONTEXT pResponse)
{
    PVOID tmp;

    if (!pResponse->List)
        return SzpInitResponseContext(pResponse, 10);

    tmp = SzpReAlloc(
        pResponse->List, 
        pResponse->Size * sizeof(RESPONSE_KEYVALUE), 
        10 * sizeof(RESPONSE_KEYVALUE)
    );

    if (!tmp)
        return FALSE;

    pResponse->Size += 10;
    pResponse->List = (PRESPONSE_KEYVALUE)tmp;

    return TRUE;
}

void SzpFreeResponseContext(PRESPONSE_CONTEXT pResponse)
{
    if (!pResponse->List)
        return;

    for (ULONG i = 0; i < pResponse->Length; i++)
    {
        if (pResponse->List[i].Value)
            SzpFree(pResponse->List[i].Value);

        if (pResponse->List[i].Properties)
            SzpFree(pResponse->List[i].Properties);
    }

    SzpFree(pResponse->List);
    memset(pResponse, 0, sizeof(RESPONSE_CONTEXT));
}


BOOL SzpReadKeyFromResponseContextEx
(
    PRESPONSE_CONTEXT pResponse, 
    PCHAR key, 
    PCHAR buffer, 
    ULONG bufSize, 
    ULONG *pValueLength,
    PKEY_PROPERTY *pProperties,
    ULONG *pPropLength
)
{
    PRESPONSE_KEYVALUE pKv;
    BOOL mriDisabled = pResponse->MRI < 0;
    
    for (LONG i = mriDisabled ? 0 : pResponse->MRI; 
        i < pResponse->Length; 
        i++)
    {
        if (!mriDisabled)
        {
            if (i == pResponse->Length)
                return FALSE;
        }

        pKv = &pResponse->List[i];

        if (!_stricmp(pKv->Key, key))
        {
            if (pProperties && pKv->PropertyLength>0)
            {
                *pProperties = pKv->Properties;
                *pPropLength = pKv->PropertyLength;
            }

            if (bufSize < pKv->ValueLength + 1)
            {
                if (pValueLength)
                    *pValueLength = pKv->ValueLength;

                return FALSE;
            }

            if (!pKv->Value)
            {
                if (pValueLength)
                    *pValueLength = 0;

                goto exitWithSuccess;
            }

            memset(buffer, 0, bufSize);
            strncpy(buffer, pKv->Value, pKv->ValueLength);

            if (pValueLength)
                *pValueLength = pKv->ValueLength;

        exitWithSuccess:

            if (!mriDisabled)
            {
                pResponse->MRI = i;
            }

            return TRUE;
        }
    }

    return FALSE;
}

BOOL SzpReadKeyFromResponseContext
(
    PRESPONSE_CONTEXT pResponse,
    PCHAR key,
    PCHAR buffer,
    ULONG bufSize,
    ULONG *pValueLength
)
{
    return SzpReadKeyFromResponseContextEx(pResponse, key, buffer, bufSize, pValueLength,NULL,NULL);
}

BOOL SzpGetProperty(const PCHAR name, PKEY_PROPERTY props, ULONG propLen, PCHAR buffer, ULONG bufSize)
{
    for (ULONG i = 0; i < propLen; i++)
    {
        if (!_stricmp(props[i].Name, name))
        {
            if (strlen(props[i].Value) > bufSize - 1)
                return FALSE;

            memset(buffer, 0, bufSize);
            strcpy(buffer, props[i].Value);
            return TRUE;
        }
    }

    return FALSE;
}

#define PPS_WHITESPACE  1
#define PPS_QUOTE       2
#define PPS_KEY_DONE    4

BOOL SzpParseProperty(PCHAR content, ULONG length, PCHAR key, ULONG keyBufSize, PKEY_PROPERTY *props, ULONG *propLength)
{
    DYNAMIC_ARRAY_OF_TYPE dynArr;
    PKEY_PROPERTY propList;
    CHAR buf[128] = { 0 };
    PCHAR p;
    ULONG i=0,propIndex=0;
    ULONG state = 0;
    BOOL result = FALSE;

    if (!props || !propLength)
        return FALSE;

    *props = NULL;
    *propLength = 0;

    if (length > sizeof(buf) - 1)
        return FALSE;

    strncpy(buf, content, length);

    p = strstr(buf, " ");

    if (!p)
    {
        if (length > keyBufSize - 1)
        {
            return FALSE;
        }

        strncpy(key, content, length);

        return TRUE;
    }

    strncpy(key, buf, p - buf);

    length -= (p - buf);

    if (!HlpInitDynamicArray((PBYTE *)&propList, sizeof(KEY_PROPERTY), 4, &dynArr))
        return FALSE;


    p = content + (p - buf);
    memset(buf, 0, sizeof(buf));

    while (length && *p != '\0')
    {
        if (*p == '=')
        {
            if (!(state & PPS_KEY_DONE))
            {
                if (HlpNeedsExtend(&dynArr))
                {
                    if (!HlpExtendDynamicArray(&dynArr, 5))
                        break;
                }

                if (i - 1 > sizeof(propList[propIndex].Name))
                    break;

                strncpy(propList[propIndex].Name, buf, i);
                memset(buf, 0, sizeof(buf));
                i = 0;
                state |= PPS_KEY_DONE;
            }
            else
            {
                //unexpected =
                break;
            }
        }
        else if (*p == ' ')
        {
            state |= PPS_WHITESPACE;
        }
        else if (*p == '\"')
        {
            if (!(state & PPS_QUOTE))
                state |= PPS_QUOTE;
            else
            {
                //second quote char arrived. ve can put buffer into value part
                state &= ~PPS_QUOTE;
                strncpy(propList[propIndex++].Value, buf, i);
                memset(buf, 0, sizeof(buf));
                i = 0;
                state = 0;

                dynArr.index++;

            }
        }
        else
        {
            //if quote char is open, put whole character into the buffer
            if (!(state & PPS_QUOTE))
            {
                //ignore previously readed part if got whitespace
                if (state & PPS_WHITESPACE)
                {
                    memset(buf, 0, sizeof(buf));
                    i = 0;
                    state &= ~PPS_WHITESPACE;
                }
            }

            buf[i++] = *p;
        }

        p++;
        length--;
    }

    result = (i == 0 && state == 0);

    if (!result)
    {
        HlpFreeDynamicArray(&dynArr);
        return FALSE;
    }

    *propLength = propIndex;
    *props = propList;

    return TRUE;
}

BOOL SzpParseResponse(PBRIDGE_TRANSPORT pTransport, PRESPONSE_CONTEXT response)
{
    CHAR tagEnd[32];
    PCHAR pBeg,pEnd;
    PRESPONSE_KEYVALUE pKv;
    PKEY_PROPERTY props=NULL;
    ULONG propLen=0;

    if (!(pTransport->Status & BTF_ACTIVE))
        return FALSE;

    if (!SzpInitResponseContext(response, 10))
        return FALSE;

    DASSERT(pTransport->Buffer.buffer != NULL);

    pBeg = pEnd = (PCHAR)pTransport->Buffer.buffer;

    while (*pBeg != '\0')
    {
        pBeg = strstr(pBeg, "<");

        if (!pBeg)
            break;

        pEnd = strstr(pBeg, ">");

        if (!pEnd)
            break;

        if (pEnd < pBeg)
            break;

        if (*(pEnd + 1) == '\0')
            break;

        pBeg++;

        if (response->Size == response->Length)
        {
            if (!SzpExtendResponseContext(response))
                break;
        }

        pKv = &response->List[response->Length];

        SzpParseProperty(pBeg, pEnd - pBeg, pKv->Key, sizeof(pKv->Key), &props, &propLen);

        if (props)
        {
            pKv->Properties = props;
            pKv->PropertyLength = propLen;
        }

        memset(tagEnd, 0, sizeof(tagEnd));
        sprintf(tagEnd, "</%s>", pKv->Key);

        pBeg = pEnd + 1;

        pEnd = strstr(pBeg, tagEnd);

        if (!pEnd)
            break;

        pKv->ValueLength = pEnd - pBeg;

        if (pKv->ValueLength > 0)
        {
            pKv->Value = (PCHAR)SzpAlloc(pKv->ValueLength + 1);

            if (!pKv->Value)
            {
                SzpFreeResponseContext(response);
                return FALSE;
            }

            strncpy(pKv->Value, pBeg, pKv->ValueLength);
        }

        response->Length++;

        pBeg += pKv->ValueLength + strlen(tagEnd);
        
    }

    if (response->Length == 0)
    {
        SzpFreeResponseContext(response);
        return FALSE;
    }

    return TRUE;
}


BOOL SzpWriteBlock(PBRIDGE_TRANSPORT pTransport, const PCHAR blockName, PCHAR data, ULONG dataLen)
{
    ULONG blockNameLen;
    CHAR buf[128];

    if (!dataLen)
        return FALSE;

    blockNameLen = strlen(blockName);

    sprintf(buf, "<%s>", blockName);
    
    RbWriteBridgeTransport(pTransport, buf, blockNameLen + 2);
    
    RbWriteBridgeTransport(pTransport, data, dataLen);

    sprintf(buf, "</%s>", blockName);
    RbWriteBridgeTransport(pTransport, buf, blockNameLen + 3);

    return TRUE;
}

BOOL SzpSetType(PBRIDGE_TRANSPORT pTransport, BYTE type)
{
    return SzpWriteBlock(pTransport, "RequestType", SzpReqTypeString[type], strlen(SzpReqTypeString[type]));
}

BOOL SzAddEntry(PSOZLUK_ENTRY_CONTEXT entry)
{
    BOOL status = FALSE;
    CHAR buf[32];
    BRIDGE_TRANSPORT rt = { 0 };
    RESPONSE_CONTEXT rctx = { 0 };

    if (strlen(entry->Baslik) > MAX_BASLIK_LEN)
        return FALSE;

    if (strlen(entry->Suser) > MAX_SUSER_LEN)
        return FALSE;

    if (!RbInitTransport(&rt, entry->DescLength + 512))
        return FALSE;

    SzpSetType(&rt, RT_ENTRY_ADD);

    SzpWriteBlock(&rt, "Baslik", entry->Baslik, strlen(entry->Baslik));
    SzpWriteBlock(&rt, "Suser", entry->Suser, strlen(entry->Suser));
    SzpWriteBlock(&rt, "Date", entry->Date, strlen(entry->Date));
    SzpWriteBlock(&rt, "Desc", entry->Desc, entry->DescLength);

    RbPassRequestToBackend(&rt);
    RbFlushTransport(&rt);

    if (!RbReadResponseFromBackend(&rt))
        goto oneWay;

    if (!SzpParseResponse(&rt, &rctx))
        goto oneWay;

    if (!SzpReadKeyFromResponseContext(&rctx, "Status", buf, sizeof(buf),NULL))
        goto oneWay;

    status = _stricmp(buf, "Ok") == 0;

    if (status)
    {
        if (SzpReadKeyFromResponseContext(&rctx, "BaslikId", buf, sizeof(buf), NULL))
            entry->BaslikId = strtoul(buf, NULL, 10);

    }

oneWay:

    RbFreeTransport(&rt);
    SzpFreeResponseContext(&rctx);

    return status;
}

BOOL SzGetSuserPassword(PCHAR suser, PCHAR buffer, ULONG bufSize)
{
    BOOL status = FALSE;
    BRIDGE_TRANSPORT rt = { 0 };
    RESPONSE_CONTEXT rctx = { 0 };

    if (!RbInitTransport(&rt, 128))
        return FALSE;

    SzpSetType(&rt, RT_USER_GET_PASS);

    SzpWriteBlock(&rt, "Suser", suser, strlen(suser));

    RbPassRequestToBackend(&rt);
    RbFlushTransport(&rt);
    
    if (!RbReadResponseFromBackend(&rt))
        goto oneWay;
    
    if (!SzpParseResponse(&rt, &rctx))
    {
        goto oneWay;
    }

    if (!SzpReadKeyFromResponseContext(&rctx, "Password", buffer, bufSize,NULL))
        goto oneWay;
    
    status = TRUE;

oneWay:

    RbFreeTransport(&rt);
    SzpFreeResponseContext(&rctx);

    return status;
}

BOOL SzAuthSuser(PCHAR suser, PCHAR password)
{
    CHAR buffer[32] = { 0 };
    BOOL status = FALSE;
    BRIDGE_TRANSPORT rt = { 0 };
    RESPONSE_CONTEXT rctx = { 0 };

    if (!RbInitTransport(&rt, 128))
        return FALSE;

    SzpSetType(&rt, RT_AUTH_SUSER);

    SzpWriteBlock(&rt, "Suser", suser, strlen(suser));
    SzpWriteBlock(&rt, "Pass", password, strlen(password));

    RbPassRequestToBackend(&rt);
    RbFlushTransport(&rt);

    if (!RbReadResponseFromBackend(&rt))
        goto oneWay;

    if (!SzpParseResponse(&rt, &rctx))
    {
        goto oneWay;
    }

    if (!SzpReadKeyFromResponseContext(&rctx, "AuthStatus", buffer, sizeof(buffer), NULL))
        goto oneWay;


    SAPILOG("User authentication result: %s", buffer);

    status = _stricmp(buffer, "AuthSuccess") == 0;

oneWay:

    RbFreeTransport(&rt);
    SzpFreeResponseContext(&rctx);

    return status;
}

BOOL SzpReadEntriesFromResponseContext(LONG recordCount,PRESPONSE_CONTEXT rctx, PSOZLUK_ENTRY_CONTEXT *pEntries)
{
    CHAR buf[64];
    PKEY_PROPERTY props = NULL;
    ULONG propLen = 0;
    PSOZLUK_ENTRY_CONTEXT entries, entry;

    entries = (PSOZLUK_ENTRY_CONTEXT)SzpAlloc(recordCount * sizeof(SOZLUK_ENTRY_CONTEXT));

    if (!entries)
        goto oneWay;


    ENABLE_MVR(rctx);

    for (LONG i = 0; i < recordCount; i++)
    {
        entry = &entries[i];
        
        if (!SzpReadKeyFromResponseContextEx(
            rctx, "Baslik", entry->Baslik, MAX_BASLIK_SIZE, NULL, &props, &propLen)
            )
        {
            continue;
        }

        if (propLen > 0)
        {
            if (SzpGetProperty("RepCount", props, propLen, buf, sizeof(buf)))
            {
                entry->RepCount = strtoul(buf, NULL, 10);
            }

            propLen = 0;
        }

        SzpReadKeyFromResponseContext(rctx, "Suser", entry->Suser, MAX_SUSER_SIZE, NULL);

        if (strlen(entry->Suser) == 0)
        {
            //fill suser name with junk to fake sozluk-cgi read sequence
            memset(entry->Suser, '#', MAX_SUSER_LEN);
        }

        SzpReadKeyFromResponseContext(rctx, "Date", entry->Date, MAX_DATE_SIZE, NULL);

        SzpReadKeyFromResponseContext(rctx, "Desc", NULL, 0, &entry->DescLength);

        if (entry->DescLength > 0)
        {
            if (MAX_FIXED_DESC_LEN > entry->DescLength)
                entry->Desc = entry->DescFixedBuf;
            else
            {
                entry->Desc = (PCHAR)SzpAlloc(entry->DescLength + 1 + NEW_LINE_LEN);
            }

            SzpReadKeyFromResponseContext(rctx, "Desc", entry->Desc, entry->DescLength + 1, NULL);
        }
        else
        {
            entry->Desc = entry->DescFixedBuf;
            memset(entry->Desc, 0, sizeof(entry->DescFixedBuf));
            entry->Desc[0] = '#';
            entry->DescLength = 1;
        }
    }

    *pEntries = entries;

oneWay:

    return TRUE;
}

BOOL SzSearch
(
    PCHAR index, 
    PCHAR content, 
    PCHAR suser, 
    PCHAR beginDate, 
    PCHAR endDate,
    PCHAR pagerHash,
    INT pageNum,
    PINDEX_QUERY_RESULT pResult
)
{
    PSOZLUK_ENTRY_CONTEXT entry = NULL;
    ULONG queryCount = 0;
    CHAR buf[64] = { 0 };
    BRIDGE_TRANSPORT rt = { 0 };
    RESPONSE_CONTEXT rctx = { 0 };

    if (!RbInitTransport(&rt, 128))
        return FALSE;

    SzpSetType(&rt, RT_SEARCH);

    if (index)
        SzpWriteBlock(&rt, "index", index, strlen(index));
    
    if (content)
        SzpWriteBlock(&rt, "term", content, strlen(content));
    
    if (suser)
        SzpWriteBlock(&rt, "suser", suser, strlen(suser));
    
    if (beginDate)
        SzpWriteBlock(&rt, "date", beginDate, strlen(beginDate));
    
    if (endDate)
        SzpWriteBlock(&rt, "todate", endDate, strlen(endDate));

    if (pageNum != -1)
    {
        _itoa(pageNum, buf, 10);
        SzpWriteBlock(&rt, "pagenum", buf, strlen(buf));
    }
    
    if (pagerHash != NULL)
    {
        SzpWriteBlock(&rt, "ph", pagerHash, strlen(pagerHash));
    }

    RbPassRequestToBackend(&rt);
    RbFlushTransport(&rt);

    RbFreeTransport(&rt);
    RtlZeroMemory(&rt, sizeof(BRIDGE_TRANSPORT));
    
    if (!RbReadResponseFromBackend(&rt))
        goto oneWay;

    if (!SzpParseResponse(&rt, &rctx))
    {
        goto oneWay;
    }

    if (!SzpReadKeyFromResponseContext(&rctx, "LogicalEntryCount", buf, sizeof(buf),NULL))
    {
        goto oneWay;
    }

    pResult->AffectedLogicalRecordCount = strtoul(buf, NULL, 10);

    if (pResult->AffectedLogicalRecordCount == 0)
        goto oneWay;

    if (!SzpReadKeyFromResponseContext(&rctx, "PhysicalEntryCount", buf, sizeof(buf), NULL))
    {
        goto oneWay;
    }

    pResult->AffectedPhysicalRecordCount = strtoul(buf, NULL, 10);

    SzpReadKeyFromResponseContext(&rctx, "TotalRecordCount", buf, sizeof(buf), NULL);
    pResult->TotalRecordCount = strtoul(buf, NULL, 10);

    SzpReadKeyFromResponseContext(&rctx, "CurrentPageNum", buf, sizeof(buf), NULL);
    pResult->CurrentPageNumber = strtoul(buf, NULL, 10);

    SzpReadKeyFromResponseContext(&rctx, "TotalPageCount", buf, sizeof(buf), NULL);
    pResult->TotalPageCount = strtoul(buf, NULL, 10);

    if (SzpReadKeyFromResponseContext(&rctx, "PagerHash", buf, sizeof(buf), NULL))
        strncpy(pResult->PagerHash, buf, 32);

    SzpReadEntriesFromResponseContext(pResult->AffectedLogicalRecordCount, &rctx, &pResult->Entries);

oneWay:

    RbFreeTransport(&rt);
    SzpFreeResponseContext(&rctx);

    return TRUE;
}

BOOL SzGetEntriesByBaslik(PCHAR baslik, INT pageNumber, UINT baslikId, BOOL latest, PENTRY_VIEW_QUERY_RESULT pResult)
{
    PSOZLUK_ENTRY_CONTEXT entries = NULL, entry = NULL;
    CHAR buf[64] = { 0 };
    BRIDGE_TRANSPORT rt = { 0 };
    RESPONSE_CONTEXT rctx = { 0 };

    if (!RbInitTransport(&rt, 128))
        return FALSE;

    SzpSetType(&rt, RT_ENTRY_GET);

    SzpWriteBlock(&rt, "baslik", baslik, strlen(baslik));
    
    _itoa(pageNumber, buf, 10);
    SzpWriteBlock(&rt, "pagenum", buf, strlen(buf));

    if (baslikId > 0)
    {
        memset(buf, 0, sizeof(buf));
        _itoa(baslikId, buf, 10);
        SzpWriteBlock(&rt, "bid", buf, strlen(buf));
    }

    if (latest)
    {
        SzpWriteBlock(&rt, "latest", "1", 1);
    }

    RbPassRequestToBackend(&rt);
    RbFlushTransport(&rt);

    RbFreeTransport(&rt);
    RtlZeroMemory(&rt, sizeof(BRIDGE_TRANSPORT));

    if (!RbReadResponseFromBackend(&rt))
        goto oneWay;

    if (!SzpParseResponse(&rt, &rctx))
    {
        goto oneWay;
    }


    SzpReadKeyFromResponseContext(&rctx, "RecordCount", buf, sizeof(buf), NULL);
    pResult->AffectedRecordCount = strtoul(buf, NULL, 10);

    SzpReadKeyFromResponseContext(&rctx, "CurrentPageNum", buf, sizeof(buf), NULL);
    pResult->CurrentPageNumber = strtoul(buf, NULL, 10);

    SzpReadKeyFromResponseContext(&rctx, "TotalPageCount", buf, sizeof(buf), NULL);
    pResult->TotalPageCount = strtoul(buf, NULL, 10);

    SzpReadKeyFromResponseContext(&rctx, "RecordsPerPage", buf, sizeof(buf), NULL);
    pResult->RecordsPerPage = (UCHAR)atoi(buf);

    SzpReadKeyFromResponseContext(&rctx, "BaslikId", buf, sizeof(buf), NULL);
    pResult->BaslikId = strtoul(buf, NULL, 10);

    SzpReadKeyFromResponseContext(&rctx, "Baslik", buf, sizeof(buf), NULL);
    strcpy(pResult->Baslik, buf);

    SzpReadEntriesFromResponseContext(pResult->AffectedRecordCount, &rctx,&pResult->Entries);

oneWay:

    RbFreeTransport(&rt);
    SzpFreeResponseContext(&rctx);

    return TRUE;
}


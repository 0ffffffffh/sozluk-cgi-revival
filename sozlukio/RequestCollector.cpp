#include "stdafx.h"
#include "RequestCollector.h"
#include "sozlukio.h"
#include "helper.h"
#include <stdlib.h>

#define ENVBUF_SIZE 4096

extern PCHAR SioCachedPostContent;
extern ULONG SioCachedPostContentLen;


extern PCHAR SioCachedEnvContent;
extern ULONG SioCachedEnvContentLen;


/*
add.exe 
word (baslik)
nick (suser)
password (suser pass)
desc (content)

index.exe
i (index identifier. * - alpha numerics, [a-z], all - whole topics, 
search (specific search keyword)
date (search begin date)
todate (search end date)
author (content owner)

*/

ULONG RcpAlreadyRead = FALSE;

#define RcAlloc(size) HlpAlloc(size)
#define RcFree(mem) HlpFree(mem)

void RciDestroyRequestObject(PSOZLUK_REQUEST request)
{
    for (LONG i = 0; i < request->KvCount; i++)
    {
        RcFree(request->KvList[i].value);
    }

    RtlZeroMemory(request, sizeof(SOZLUK_REQUEST));
}

BOOL RciParseQueryString(PCHAR queryString, ULONG length, PSOZLUK_REQUEST request)
{
    PREQUEST_KEYVALUE pKv;
    LONG b=0, e=0;
    PCHAR pstr;
    
    RtlZeroMemory(request, sizeof(SOZLUK_REQUEST));

    pKv = &request->KvList[request->KvCount];

    for (pstr = queryString ;; pstr++)
    {
        if (*pstr == '=')
        {
            e = pstr - queryString;

            if (e - b > sizeof(pKv->key) - 1)
            {
                RciDestroyRequestObject(request);
                DLOG("Too long key\n");
                return FALSE;
            }

            strncpy(pKv->key, queryString + b, e - b);


            b = e;

            if (b < length)
                b++;

        }
        else if (*pstr == '&' || *pstr == '\0')
        {
            e = pstr - queryString;

            pKv->valueLength = e - b;
            pKv->value = (PCHAR)RcAlloc(pKv->valueLength + 1);

            if (!pKv->value)
            {
                RciDestroyRequestObject(request);
                return FALSE;
            }

            strncpy(pKv->value, queryString + b, pKv->valueLength);


            b = e;

            if (b < length)
                b++;

            if (*pstr == '\0')
            {
                request->KvCount++;
                break;
            }
            else
            {
                if (request->KvCount == sizeof(request->KvList) / sizeof(request->KvList[0]))
                {
                    DLOG("Too much key-value in the qs / Suspicious behaviour");
                    RciDestroyRequestObject(request);
                    return FALSE;
                }

                pKv = &request->KvList[++request->KvCount];

                
            }

            
        }
    }

    return request->KvCount > 0;
}



BOOL RcReadRequest(PSOZLUK_REQUEST request)
{
    CHAR contentLenBuf[32] = { 0 };
    CHAR queryStringBuf[ENVBUF_SIZE] = { 0 };
    PCHAR queryString = NULL;

    DWORD qsLen = 0, contentLen = 0;

    if (InterlockedCompareExchange((volatile ULONG *)&RcpAlreadyRead, TRUE, FALSE) == TRUE)
    {
        return TRUE;
    }

    qsLen = Hook_GetEnvironmentVariableA("QUERY_STRING_INTERNAL", queryStringBuf, sizeof(queryStringBuf));
    Hook_GetEnvironmentVariableA("CONTENT_LENGTH", contentLenBuf, sizeof(contentLenBuf));

    contentLen = strtoul(contentLenBuf, NULL, 10);

    if (qsLen > 0)
    {

        if (ENVBUF_SIZE < qsLen)
        {
            //Someone has sent too long query string. Fuck it.
            return FALSE;
        }

        queryString = queryStringBuf;
        
    }
    else if (contentLen > 0)
    {

        if (!SioCachedPostContent)
        {
            Hook_ReadFile(GetStdHandle(STD_INPUT_HANDLE), NULL, 0, NULL, NULL);
        }

        if (SioCachedPostContent)
        {
            queryString = SioCachedPostContent;
            qsLen = SioCachedPostContentLen;
        }
        else
        {
            DLOG("Something went wrong");
        }
    }
    else
        return FALSE;


    if (!RciParseQueryString(queryString, qsLen, request))
    {
        DLOG("parse error");
        return FALSE;
    }

    return TRUE;
}
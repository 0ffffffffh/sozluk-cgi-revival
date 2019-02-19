#include "stdafx.h"
#include "helper.h"
#include <time.h>
#include <stdlib.h>
#include <stdio.h>

#include <shlwapi.h>
#pragma comment(lib,"shlwapi.lib")

extern PBYTE HkpCalloc(ULONG count, ULONG itemSize);

extern PVOID HkpReAlloc(PVOID old, ULONG oldSize, ULONG amount);

extern PVOID HkpAlloc(ULONG size);

extern BOOL RciParseQueryString(PCHAR queryString, ULONG length, PSOZLUK_REQUEST request);

extern void RciDestroyRequestObject(PSOZLUK_REQUEST request);

extern void HkpFree(PVOID mem);

#ifndef TRACK_ALLOC

PVOID HlpAlloc(ULONG size)
{
    return HkpAlloc(size);
}

VOID HlpFree(PVOID mem)
{
    HkpFree(mem);
}


PVOID HlpReAlloc(PVOID mem, ULONG oldSize, ULONG amount)
{
    return HkpReAlloc(mem, oldSize, amount);
}

PVOID HlpReAlloc2(PVOID mem, ULONG newSize)
{
    return HkpReAlloc(mem, 0, newSize);
}

#endif

void HlpxDbgPrint(LPCSTR format, ...)
{
    char buffer[1024];
    va_list va;
    va_start(va, format);

    memset(buffer, 0, sizeof(buffer));
    vsnprintf(buffer, sizeof(buffer), format, va);

    OutputDebugStringA(buffer);

    va_end(va);
}

#include <WinSock2.h>
#pragma comment(lib,"Ws2_32.lib")

SOCKET HlppLogsock = 0;
struct sockaddr_in HlppEndpointAddr;


void HlpInitLogClient()
{
    WSADATA wsa;

    WSAStartup(MAKEWORD(2, 2), &wsa);

    HlppLogsock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);

    HlppEndpointAddr.sin_family = AF_INET;
    HlppEndpointAddr.sin_port = htons(2019);
    HlppEndpointAddr.sin_addr.S_un.S_addr = inet_addr("127.0.0.1");

}

void HlpUninitLogClient()
{
    closesocket(HlppLogsock);
    WSACleanup();
}

void HlpSendLogString(LPCSTR format, ...)
{
    char buffer[1024];
    LONG len;
    va_list va;
    va_start(va, format);

    memset(buffer, 0, sizeof(buffer));
    len = vsnprintf(buffer, sizeof(buffer), format, va);

    va_end(va);
    
#if 1
    OutputDebugStringA(buffer);
#endif

    sendto(HlppLogsock, buffer, len, 0, (struct sockaddr *)&HlppEndpointAddr, sizeof(HlppEndpointAddr));
}

ULONG HlpRemoveString(PCHAR str, ULONG length, PCHAR findStr)
{
    ULONG newLen = length;
    PCHAR p, pend;
    ULONG findStrLen = strlen(findStr);

    p = strstr(str, findStr);

    while (p)
    {
        pend = p + findStrLen;

        if (*pend == '\0')
        {
            memset(p, 0, findStrLen);
            return newLen - findStrLen;
        }

        memmove(p, pend, newLen - (pend - str));
        newLen -= findStrLen;

        *(str + newLen) = '\0';

        p = strstr(p, findStr);
    }

    return newLen;
}

LONG HlpStrPos(PCHAR str, PCHAR substr)
{
    ULONG pos = (ULONG)strstr(str, substr);

    if (!pos)
        return -1;

    return (LONG)(pos - ((ULONG)str));
}

PCHAR HlpTokenize(PCHAR str, PCHAR delim, ULONG flag, PTOKENIZE_CONTEXT tokCtx)
{
    PCHAR tok, locator;

    if (str)
    {
        tokCtx->data = str;
        tokCtx->flag = flag;
        tokCtx->delim = delim;
        tokCtx->pos = 0;
    }

    if (!str)
    {
        if (tokCtx->flag & TOKF_DONE)
        {
            return NULL;
        }

        *(tokCtx->data + tokCtx->pos) = tokCtx->saveChr;
        tokCtx->pos += strlen(tokCtx->delim);

    }

    tok = tokCtx->data + tokCtx->pos;

    if (*tok == '\0')
    {
        //end of string
        tokCtx->flag |= TOKF_DONE;
        
        if (!(tokCtx->flag & TOKF_SKIP_EMPTY))
            return tok;

        return NULL;
    }

    locator = strstr(tok, tokCtx->delim);

    if (locator)
    {
        tokCtx->pos = locator - tokCtx->data;
        tokCtx->saveChr = *locator;
        *locator = '\0';
    }
    else
    {
        tokCtx->flag |= TOKF_DONE;
        return tok;
    }

    if (*tok == '\0' && !(tokCtx->flag & TOKF_SKIP_EMPTY))
    {
        return tok;
    }

    return tok;
}

//Dynamic array support routines
BOOL HlpInitDynamicArray(PBYTE *container,ULONG sizeOfType, ULONG initialSize, PDYNAMIC_ARRAY_OF_TYPE arrPtr)
{
    arrPtr->data = (PBYTE)HlpAlloc(initialSize * sizeOfType);

    if (!arrPtr->data)
        return FALSE;

    arrPtr->container = container;
    *container = arrPtr->data;

    arrPtr->sizeOfType = sizeOfType;
    arrPtr->arraySize = initialSize;
    arrPtr->index = 0;

    return TRUE;
}

BOOL HlpExtendDynamicArray(PDYNAMIC_ARRAY_OF_TYPE arrPtr, ULONG amount)
{
    PBYTE newPtr = (PBYTE)HlpReAlloc(arrPtr->data, arrPtr->arraySize * arrPtr->sizeOfType, amount * arrPtr->sizeOfType);

    if (!newPtr)
        return FALSE;

    arrPtr->data = newPtr;
    *arrPtr->container = newPtr;
    arrPtr->arraySize += amount;

    return TRUE;
}

VOID HlpFreeDynamicArray(PDYNAMIC_ARRAY_OF_TYPE arrPtr)
{
    HlpFree(arrPtr->data);
    memset(arrPtr, 0, sizeof(DYNAMIC_ARRAY_OF_TYPE));
}


//Auto expandable buffer support routines
BOOL HlppExtendAutoBuffer(PAUTO_BUFFER pBuffer, ULONG amount)
{
    PBYTE newBuf;

    newBuf = (PBYTE)HlpReAlloc(pBuffer->buffer, pBuffer->size, amount);

    if (!newBuf)
        return FALSE;

    pBuffer->size += amount;
    pBuffer->buffer = newBuf;

    return TRUE;
}

BOOL HlpInitializeAutoBuffer(PAUTO_BUFFER pBuffer, ULONG initialSize)
{
    pBuffer->buffer = (PBYTE)HlpAlloc(initialSize);

    if (!pBuffer->buffer)
    {
        DLOG("File wr buffer not be allocated");
        return FALSE;
    }

    pBuffer->size = initialSize;
    pBuffer->isDynamic = FALSE;

    return TRUE;
}

PAUTO_BUFFER HlpCreateAutoBuffer(ULONG initialSize)
{
    PAUTO_BUFFER pBuf;

    pBuf = (PAUTO_BUFFER)HlpAlloc(sizeof(AUTO_BUFFER));

    if (!pBuf)
    {
        DLOG("File wr buffer object not be allocated");
        return NULL;
    }

    if (!HlpInitializeAutoBuffer(pBuf, initialSize))
    {
        HlpFree(pBuf);
        return NULL;
    }

    pBuf->isDynamic = TRUE;

    return pBuf;
}


void HlpDisposeAutoBuffer(PAUTO_BUFFER pBuffer)
{
    if (pBuffer)
    {
        if (pBuffer->buffer)
            HlpFree(pBuffer->buffer);

        if (pBuffer->isDynamic)
            HlpFree(pBuffer);
    }
}

PBYTE HlpTakeBufferOwnershipAndDestroyBufferObject(PAUTO_BUFFER buffer)
{
    PBYTE buf = buffer->buffer;

    buffer->buffer = NULL;

    HlpDisposeAutoBuffer(buffer);

    return buf;
}

BOOL HlpWriteIntoAutoBuffer(PAUTO_BUFFER pBuffer, PVOID data, ULONG size)
{
    if (pBuffer->size - pBuffer->pos < size)
    {
        if (!HlppExtendAutoBuffer(pBuffer, size + 256))
        {
            DLOG("buffer extend failed!.");
            return FALSE;
        }
    }

    memcpy(pBuffer->buffer + pBuffer->pos, data, size);
    pBuffer->pos += size;

    return TRUE;
}

DWORD HlpReadFromNativeFileHandle(PAUTO_BUFFER pBuffer, HANDLE handle, ULONG readSize)
{
    DWORD readedLen = 0;

    if (readSize > pBuffer->size - pBuffer->pos)
    {
        if (!HlppExtendAutoBuffer(pBuffer, readSize + 256))
            return 0;
    }

    if (!ReadFile(handle, pBuffer->buffer + pBuffer->pos, readSize, &readedLen, NULL))
    {
        DLOG("cant be read from native handle");
        return 0;
    }

    pBuffer->pos += readedLen;

    return readedLen;
}

BOOL HlpInsertIntoAutoBuffer(PAUTO_BUFFER pBuffer, ULONG index, PVOID data, ULONG size, ULONG overwriteSize)
{
    if (overwriteSize > size)
        return FALSE;

    if (pBuffer->size - pBuffer->pos < size - overwriteSize)
    {
        if (!HlppExtendAutoBuffer(pBuffer, size + 256))
        {
            DLOG("buf extend fail");
            return FALSE;
        }
    }

    memmove(
        pBuffer->buffer + index + size - overwriteSize, 
        pBuffer->buffer + index + overwriteSize, 
        pBuffer->pos - index - overwriteSize
    );

    memcpy(pBuffer->buffer + index, data, size);

    pBuffer->pos += size - overwriteSize;

    *(pBuffer->buffer + pBuffer->pos) = '\0';

    return TRUE;
}

/*
gen-delims  = ":" / "/" / "?" / "#" / "[" / "]" / "@"

sub-delims  = "!" / "$" / "&" / "'" / "(" / ")"
                  / "*" / "+" / "," / ";" / "="
*/

const char *ENC_RESERVED = ":/?#[]@!$&'()*+,;=";
const char *ENC_UNRESERVED = "-._~";

//ISO_8859_9 encoded TR character table
//following Turkish characters are compatible with 
//the Windows-1254 encoding 
//which are defined as ISO 8859-9
struct ISO_8859_9_TR_CHARS
{
    CHAR ansiChar;
    union {
        CHAR cval;
        UCHAR ucval;
    }encChar;
    
    USHORT uniChar;
}TR_CHARS[12] = 
{
    {'C', 0xC7, 0x00C7},
    {'G', 0xD0, 0x011E}, 
    {'O', 0xD6, 0x00D6},
    {'U', 0xDC, 0x00DC},
    {'I', 0xDD, 0x0130},
    {'S', 0xDE, 0x015E},
    {'c', 0xE7, 0x00E7},
    {'g', 0xF0, 0x011F},
    {'o', 0xF6, 0x00F6},
    {'u', 0xFC, 0x00FC},
    {'i', 0xFD, 0x0131},
    {'s', 0xFE, 0x015F},
};

#define EncIsAllowed(chr) (isdigit(chr) || isalnum(chr) || strchr(ENC_UNRESERVED,chr) != NULL)
#define EncIsReserved(chr) (strchr(ENC_RESERVED,chr) != NULL)

typedef struct ISO_8859_9_TR_CHARS *PISO_8859_9_TR_CHARS;

PISO_8859_9_TR_CHARS HlppMapByUnichr(WCHAR chr)
{
    for (int i = 0; i < 12; i++)
    {
        if (TR_CHARS[i].uniChar == (USHORT)chr)
            return &TR_CHARS[i];
    }

    return NULL;
}

PISO_8859_9_TR_CHARS HlppMapByAnsichr(CHAR chr)
{
    for (int i = 0; i < 12; i++)
    {
        if (TR_CHARS[i].ansiChar == chr)
            return &TR_CHARS[i];
    }

    return NULL;
}

PISO_8859_9_TR_CHARS HlppMapByEncodedChr(CHAR chr)
{
    for (int i = 0; i < 12; i++)
    {
        if (TR_CHARS[i].encChar.cval == chr)
            return &TR_CHARS[i];
    }

    return NULL;
}

PISO_8859_9_TR_CHARS HlppMapByEncodedUChr(UCHAR chr)
{
    for (int i = 0; i < 12; i++)
    {
        if (TR_CHARS[i].encChar.ucval == chr)
            return &TR_CHARS[i];
    }

    return NULL;
}

DWORD HlpDeleteString(PCHAR s, ULONG slen, ULONG index, ULONG length)
{
    if (index >= slen)
        return 0;

    if (index + length > slen)
        return 0;

    memmove(s + index, s + index + length, slen - (index + length));
    slen -= length;
    *(s + slen) = '\0';

    return slen;
}

DWORD HlpInsertString(PCHAR s, ULONG slen, ULONG index, PCHAR insertStr)
{
    ULONG islen = strlen(insertStr);

    memmove(s + index + islen, s + index, slen - index);
    memcpy(s + index, insertStr, islen);
    slen += islen;

    *(s + slen) = '\0';

    return slen;
}

DWORD HlpReplaceString(PCHAR *ps, ULONG *pSizeOfString, PCHAR find, PCHAR repl)
{
    ULONG flen, rlen;
    ULONG sizeOfString, newLength;
    LONG pos = 0, pLen = 0, maxLen;
    PCHAR s = *ps, tmp;

    sizeOfString = *pSizeOfString;

    maxLen = sizeOfString - 1;

    flen = strlen(find);
    rlen = strlen(repl);

    newLength = strlen(s);

    while ((pos = HlpStrPos(s + pLen, find)) > -1)
    {
        newLength = HlpDeleteString(s, newLength, pLen + pos, flen);

        if (rlen > flen && newLength + rlen - flen > maxLen)
        {
            tmp = (PCHAR)HlpReAlloc(s, sizeOfString, 64);

            if (!tmp)
                return newLength;

            s = tmp;
            sizeOfString += 64;
            maxLen = sizeOfString - 1;
        }

        newLength = HlpInsertString(s, newLength, pLen + pos, repl);

        pLen += pos + rlen;
    }

    *ps = s;
    *pSizeOfString = sizeOfString;

    return newLength;
}


DWORD HlpUrlEncodeAscii(PCHAR value, ULONG length, PCHAR *encodedValue, BOOL includeReserved)
{
    PISO_8859_9_TR_CHARS encTable;

    DWORD encLen = 0;
    PCHAR pEnc;
    ULONG pEncSize = ((length + 5) * 3) + 1;
    
    pEnc = (PCHAR)HlpAlloc(pEncSize);

    if (!pEnc)
        return 0;
    
    for (INT i = 0; i < length; i++)
    {
        //Workarounds to escape sozluk-cgi's incorrect querystring parsing routines

        if (value[i] == '<' || value[i] == '>')
        {
            sprintf(pEnc + encLen, "$%s;", value[i] == '<' ? "lt" : "gt");
            encLen += 4;
            continue;
        }
        else if (value[i] == '&')
        {
            strcpy(pEnc + encLen, "$amp;");
            encLen += 5;
            continue;
        }
        else if (value[i] == '=')
        {
            strcpy(pEnc + encLen, "$eq;");
            encLen += 4;
            continue;
        }
        else if (value[i] == '%')
        {
            strcpy(pEnc + encLen, "$percnt;");
            encLen += 8;
            continue;
        }
        else if (value[i] == '+')
        {
            strcpy(pEnc + encLen, "$plus;");
            encLen += 6;
            continue;
        }

        
        if (!isascii(value[i]))
        {
            encTable = HlppMapByEncodedChr(value[i]);

            if (encTable)
            {
                *(pEnc + encLen) = encTable->ansiChar;
                encLen++;
            }
            
            continue;
        }

        if (EncIsAllowed(value[i]) || (!includeReserved && EncIsReserved(value[i])))
        {
            *(pEnc + encLen) = value[i];
            encLen++;
        }
        else
        {
            sprintf(pEnc + encLen, "%%%02x", value[i]);
            encLen += 3;
        }
    }


    *encodedValue = pEnc;

    return encLen;
}

DWORD HlpUrlDecodeAsAscii(PCHAR value, ULONG length)
{
    PISO_8859_9_TR_CHARS encTable;

    CHAR buf[16] = { 0 };
    PWCHAR valueW,pw;
    PCHAR p, pbuf;
    INT radix = 10;
    LONG entValue;

    DWORD tmp;

    //first, we need to convert + to space.
    //urlunescape api handles only %xy form  
    for (LONG i = 0; i < length; i++)
    {
        if (value[i] == '+')
            value[i] = ' ';

    }
    
    //the most junk api ever in the Windows API set. piece of crap
    //But that saved my time a bit to quick unescape encoded value
    UrlUnescapeA(value, NULL, &tmp, URL_UNESCAPE_INPLACE);

    length = strlen(value);

    valueW = (PWCHAR)HlpAlloc((length + 1) * sizeof(WCHAR));

    if (!valueW)
        return 0;

    pw = valueW;

    p = value;

    for (p = value; *p != '\0'; p++)
    {
        //Some browsers can translate non-ascii chars as html entity
        //before the percentage encoding.
        if (!strncmp(p, "&#",2))
        {
            p += 2;

            radix = *p == 'x' ? 16 : 10;
            
            pbuf = strstr(p, ";");

            if (pbuf == NULL)
            {
                *pw++ = L'&';
                p -= 2;
                continue;
            }

            if (pbuf - p >= sizeof(buf))
            {
                *pw++ =  L'&';
                p -= 2;
                continue;
            }

            if (radix == 16)
                p++;

            strncpy(buf, p, pbuf - p);
            
            entValue = strtoul(buf, NULL, radix);

            memset(buf, 0, sizeof(buf));

            //dont put special chars
            if (entValue >= 32)
                *pw++ = (WCHAR)entValue;

            p = pbuf;
        }
        else
        {
            if (!isascii(*p))
            {
                encTable = HlppMapByEncodedChr(*p);

                if (encTable)
                {
                    *((USHORT *)pw) = encTable->uniChar;
                    pw++;
                }

            }
            else
            {
                *pw = *p;
                *((USHORT *)pw) &= 0x00FF;
                pw++;
            }
        }
    }

    /*
    A litte remainder to someone who wants to build and run this code.
    WC_COMPOSITECHECK depends on the system locale setting.
    So If the system's default locale is set to Turkish by default
    composition does not occur for accented letters as expected. Because turkish
    accented letters are valid in the locale and these are not need to be translated. 

    You have two choice to handle this.
    1) change your system locate. 

    2) use the PISO_8859_9_TR_CHARS table that I defined above in this file.
    map encoded value using HlppMapByEncodedChr for example.
    and put its ascii equivalent from the returned map struct. but remember
    it works for Turkish letters only.!

    */
    tmp = WideCharToMultiByte(CP_ACP, WC_COMPOSITECHECK, valueW, -1, value, length + 1, NULL, NULL);

    HlpFree(valueW);

    if (!tmp)
        return 0;

    return tmp - 1; //dont count null term 
}

DWORD HlpReEncodeAsAscii(PCHAR value, ULONG length, PCHAR *encodedValue, KEYVALUE_REENCODE_CALLBACK cb)
{
    PREQUEST_KEYVALUE pKv;
    SOZLUK_REQUEST req;
    PAUTO_BUFFER buf;
    DWORD dlen;
    PCHAR tmp;

    if (!RciParseQueryString(value, length, &req))
        return FALSE;

    buf = HlpCreateAutoBuffer(length);

    for (LONG i = 0; i < req.KvCount; i++)
    {
        pKv = &req.KvList[i];

        if (cb)
            cb(pKv, RL_INITIAL);

        HlpWriteIntoAutoBuffer(buf, pKv->key, strlen(pKv->key));
        HlpWriteIntoAutoBuffer(buf, "=", 1);

        dlen = HlpUrlDecodeAsAscii(pKv->value, pKv->valueLength);

        if (cb)
            cb(pKv, RL_AFTER_DECODE);

        dlen = HlpUrlEncodeAscii(pKv->value, dlen, &tmp,TRUE);


        pKv->valueLength = dlen;

        if (tmp != pKv->value)
        {
            HlpFree(pKv->value);
            pKv->value = tmp;
        }

        if (cb)
            cb(pKv, RL_AFTER_ENCODE);

        VERBOSE("%s re-encoded as %s", pKv->key, pKv->value);
        
        HlpWriteIntoAutoBuffer(buf, pKv->value, pKv->valueLength);


        if (i != req.KvCount - 1)
            HlpWriteIntoAutoBuffer(buf, "&", 1);
    }

    RciDestroyRequestObject(&req);

    dlen = buf->pos;

    if (buf->pos > length)
    {
        *encodedValue = (PCHAR)HlpTakeBufferOwnershipAndDestroyBufferObject(buf);
    }
    else
    {
        memset(value, 0, length);
        memcpy(value, buf->buffer, buf->pos);

        *encodedValue = value;

        HlpDisposeAutoBuffer(buf);
    }

    return dlen;
}

BOOL HlpGetFileNameFromPath(PCHAR path, PCHAR fileNameBuf, ULONG bufSize)
{
    DWORD len, i;

    len = strlen(path);

    i = len;

    if (i > 0)
    {
        i--;

        do
        {
            if (path[i] == '\\')
            {
                i++;
                len = len - i + 1;

                if (fileNameBuf == NULL)
                {
                    memmove(path, &path[i], len);
                    path[len] = '\0';
                }
                else
                {

                    if (len + 1 > bufSize)
                        return FALSE;

                    strncpy(fileNameBuf, &path[i], len);
                }

                break;
            }
            i--;
        } while (i > 0);
    }

    if (i == 0)
    {
        //looks like whole path is actually just a filename

        if (fileNameBuf != NULL)
        {
            if (len + 1 > bufSize)
                return FALSE;

            strcpy(fileNameBuf, path);
        }

    }

    return TRUE;
}

BOOL HlpGetExecutableName(PCHAR exeNameBuf, ULONG bufSize)
{
    DWORD len;

    len = GetModuleFileNameA(GetModuleHandleA(NULL), exeNameBuf, bufSize);

    if (!len)
        return FALSE;

    return HlpGetFileNameFromPath(exeNameBuf, NULL, bufSize);
}

void HlpGetCurrentDateString(CHAR *buffer, ULONG bufSize)
{
    time_t t;
    struct tm *ptm;

    t = time(NULL);

    ptm = localtime(&t);

    strftime(buffer, bufSize, "%m/%d/%Y", ptm);
}

BOOL HlpBuildEntryContextFromWriteData(PAUTO_BUFFER dataBuffer, PSOZLUK_ENTRY_CONTEXT sec)
{
    PAUTO_BUFFER descBuf;
    ULONG order = 0;
    PCHAR psz = NULL;
    PCHAR token;
    TOKENIZE_CONTEXT tokCtx;


    if (!dataBuffer->pos)
        return FALSE;

    psz = (PCHAR)dataBuffer->buffer;

    if (*psz != '~')
        return FALSE;

    psz++;

    token = HlpTokenize(psz, "\r\n", 0, &tokCtx);

    descBuf = HlpCreateAutoBuffer(256);

    if (!descBuf)
        return FALSE;

    while (token)
    {
        switch (order)
        {
        case 0:
            strcpy(sec->Baslik, token);
            break;
        case 1:
            strcpy(sec->Suser, token);
            break;
        case 2:
            HlpGetCurrentDateString(sec->Date, sizeof(sec->Date));
            break;
        default:
        {
            HlpWriteIntoAutoBuffer(descBuf, token, strlen(token));
            HlpWriteIntoAutoBuffer(descBuf, "\r\n", 2);
            break;
        }
            
        }
        token = HlpTokenize(NULL, NULL, 0, &tokCtx);

        order++;
    }

    sec->DescLength = descBuf->pos;
    sec->Desc = (PCHAR)HlpTakeBufferOwnershipAndDestroyBufferObject(descBuf);
    
    return TRUE;
}

BOOL HlppDontHang = FALSE;

BOOL HlpxHangUntilDebuggerAttach(LPCSTR func,LPCSTR hint)
{
    LONG remainder = 0;
    INT qsel;
    CHAR msgBuf[256] = { 0 };

    if (HlppDontHang)
        return FALSE;

    if (!IsDebuggerPresent())
    {

        sprintf(msgBuf, "want to hang till debugger attached for \r\n"
            "\"%s\" (%s)?\r\n"
            "yes: waits debugger\r\nno: dont wait for this time\r\ncancel: dont wait all the time",
            func,hint != NULL ? hint : "");

        qsel = MessageBoxA(NULL, msgBuf,
            "sozlukio", MB_ICONQUESTION | MB_YESNOCANCEL);

        if (qsel == IDCANCEL)
        {
            HlppDontHang = TRUE;
            return FALSE;
        }
        else if (qsel == IDNO)
            return FALSE;

        DLOG("Waiting for debugger to be attached");

        while (!IsDebuggerPresent())
        {
            if (remainder == (2000 / 10))
            {
                DLOG("Still waiting for debugger to be attached!!");
                remainder = 0;
            }
            else
                remainder++;

            Sleep(10);
        }

        DLOG("Debugger attached!.");

        DebugBreak();
    }

    return TRUE;
}
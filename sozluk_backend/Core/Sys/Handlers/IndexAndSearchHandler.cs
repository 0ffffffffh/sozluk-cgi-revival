using sozluk_backend.Core.Sys.DataStore;
using sozluk_backend.Core.Sys.Model;
using sozluk_backend.Core.Sys.Request;
using System;
using System.Collections.Generic;
using System.Text;

namespace sozluk_backend.Core.Sys.Handlers
{
    class IndexAndSearchHandler : SozlukRequestHandlerBase
    {

        public IndexAndSearchHandler(RequestObject request)
            : base(request)
        {
            
        }

        private string NormalizeTerm(string s)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var c in s)
            {
                if (char.IsLetterOrDigit(c) || c == ' ')
                    sb.Append(char.ToLower(c));
                else if (char.IsSeparator(c))
                    sb.Append(' ');
            }

            s = sb.ToString();
            sb.Clear();
            sb = null;

            return s;
        }

        private bool IsValidPagerHash(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;

            if (s.Length != 32)
                return false;

            for (int i=0;i<32;i++)
            {
                var c = char.ToLower(s[i]);

                if (!(c >= '0' && c <= '9') || !(c >= 'a' && c <= 'f'))
                    return false;

            }

            return true;
        }

        private void HandleIndexing()
        {
            int pageNum;
            string pagerHash;
            string indexVal;

            SearchAndIndexQueryResult result;

            pageNum = GetValue<int>("pagenum");
            pagerHash = GetValue<string>("ph");
            indexVal = GetValue<string> ("index");

            if (!string.IsNullOrEmpty(pagerHash) && IsValidPagerHash(pagerHash))
            {
                PushResponseItem("LogicalEntryCount", 0);
                return;
            }

            if (pageNum > 0)
                pageNum--;
            else
                pageNum = 0;

            result = SozlukDataStore.FetchBasliks(indexVal, pageNum,pagerHash);

            if (result.HasEntry)
            {
                PushResponseItem("TotalRecordCount", result.TotalRecordCount);
                PushResponseItem("LogicalEntryCount", result.LogicalRecordCount);
                PushResponseItem("PhysicalEntryCount", result.PhysicalRecordCount);
                PushResponseItem("TotalPageCount", SozlukDataStore.CalcPageCount(result.TotalRecordCount,RecordPerPageType.Basliks));
                PushResponseItem("CurrentPageNum", pageNum + 1);

                if (!string.IsNullOrEmpty(result.PagerHash))
                    PushResponseItem("PagerHash", result.PagerHash);

                foreach (var entry in result.Entries)
                    PushResponseContent(entry.GetTransportString());


                result.Entries.Clear();
                result.Entries = null;
            }
            else
            {
                PushResponseItem("LogicalEntryCount", 0);
            }

        }

        private void HandleSearch()
        {
            bool noDate;
            int pageNum = GetValue<int>("pagenum");
            string pagerHash = GetValue<string>("ph");
            string term = GetValue<string>("term");
            string suser = GetValue<string>("suser");
            DateTime beginDate = GetValue<DateTime>("date");
            DateTime endDate = GetValue<DateTime>("todate");

            noDate = beginDate == DateTime.MinValue;

            if (noDate)
                endDate = DateTime.MinValue; //no date
            else if (endDate == DateTime.MinValue)
            {
                //begin set but end not. so assume the end to the today like sozluk-cgi does
                endDate = DateTime.Now; 

                if (beginDate > endDate)
                {
                    //We are damn sure that there will be no record :P
                    PushResponseItem("LogicalEntryCount", 0);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(suser) && !Suser.IsSuserNameAllowed(suser))
            {
                PushResponseItem("LogicalEntryCount", 0);
                return;
            }

            if (!string.IsNullOrEmpty(pagerHash) && IsValidPagerHash(pagerHash))
            {
                PushResponseItem("LogicalEntryCount", 0);
                return;
            }

            if (!string.IsNullOrEmpty(term))
                term = NormalizeTerm(term);

            if (pageNum > 0)
                pageNum--;

            SearchAndIndexQueryResult result;

            result = SozlukDataStore.FetchBasliksUsingSearch(
                false, term, suser, beginDate, endDate, 
                pageNum,pagerHash,noDate);

            if (result.HasEntry)
            {
                PushResponseItem("TotalRecordCount", result.TotalRecordCount);
                PushResponseItem("LogicalEntryCount", result.LogicalRecordCount);
                PushResponseItem("PhysicalEntryCount", result.PhysicalRecordCount);
                PushResponseItem("TotalPageCount", SozlukDataStore.CalcPageCount(result.TotalRecordCount,RecordPerPageType.Basliks));
                PushResponseItem("CurrentPageNum", pageNum + 1);


                if (!string.IsNullOrEmpty(result.PagerHash))
                    PushResponseItem("PagerHash", result.PagerHash);


                foreach (var entry in result.Entries)
                    PushResponseContent(entry.GetTransportString());

                result.Entries.Clear();
                result.Entries = null;
            }
            else
            {
                PushResponseItem("LogicalEntryCount", 0);
            }
        }

        public override bool Process()
        {
            if (HasKey("term") || HasKey("suser") || HasKey("date") || HasKey("todate"))
            {
                HandleSearch();
                return true;
            }

            HandleIndexing();
            return true;
        }
    }
}

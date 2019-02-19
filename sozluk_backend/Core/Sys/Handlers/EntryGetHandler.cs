using sozluk_backend.Core.Cache;
using sozluk_backend.Core.Sys.DataStore;
using sozluk_backend.Core.Sys.Model;
using sozluk_backend.Core.Sys.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Handlers
{
    class EntryGetHandler : SozlukRequestHandlerBase
    {
        public EntryGetHandler(RequestObject req)
            : base(req)
        {

        }

        public override bool Process()
        {
            ViewQueryResult queryResult;
            int pageNumber, baslikId;
            bool latest = false;

            pageNumber = GetValue<int>("pagenum");
            baslikId = GetValue<int>("bid");

            if (HasKey("latest"))
                latest = true;

            if (baslikId < 0)
                baslikId = 0;

            if (pageNumber > 0)
                pageNumber--;
            else
                pageNumber = 0;

            if (latest && baslikId > 0)
            {
                BaslikBasicInfo bbi;

                if (CacheManager.TryGetCachedResult<BaslikBasicInfo>("BBI_" + baslikId.ToString(), out bbi))
                {
                    pageNumber = SozlukDataStore.CalcPageCount(bbi.TotalEntries,RecordPerPageType.Entries) - 1;
                }

            }

            queryResult = SozlukDataStore.FetchEntriesOfBaslik(
                GetValue<string>("baslik"),
                baslikId,
                pageNumber);

            if (queryResult.HasEntry)
            {

                PushResponseItem("RecordCount", queryResult.PhysicalRecordCount);
                PushResponseItem("TotalPageCount", SozlukDataStore.CalcPageCount(queryResult.TotalRecordCount,RecordPerPageType.Entries));
                PushResponseItem("RecordsPerPage", SozlukDataStore.RecordsPerPage);
                PushResponseItem("CurrentPageNum", pageNumber + 1);
                PushResponseItem("BaslikId", queryResult.BaslikId);
                PushResponseItem("Baslik", queryResult.Entries[0].Baslik);


                foreach (var entry in queryResult.Entries)
                    PushResponseContent(entry.GetTransportString());
            }
            else
            {
                PushResponseItem("RecordCount", 0);
            }

            return true;
        }
    }
}

using sozluk_backend.Core.Sys.DataStore;
using sozluk_backend.Core.Sys.Logging;
using sozluk_backend.Core.Sys.Model;
using sozluk_backend.Core.Sys.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Handlers
{
    class EntryAddHandler : SozlukRequestHandlerBase
    {
        private Entry entry;

        public EntryAddHandler(RequestObject request)
            : base(request)
        {
            entry = Entry.FromResponse(request);
            entry.SecureFields();
        }

        public override bool Process()
        {
            bool status = SozlukDataStore.AddEntry(entry);

            if (!status)
            {
                Log.Warning("Entry addition failed ({0})",entry.ToString());
            }

            base.PushResponseItem("Status", Helper.GetStatusString(status, "Ok", "Fail"));

            if (status)
            {
                PushResponseItem("BaslikId", entry.BaslikID);
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using sozluk_backend.Core.Sys.Logging;
using sozluk_backend.Core.Sys.Request;

namespace sozluk_backend.Core.Sys.Handlers
{
    class SozlukRequestHandlerBase : ISozlukRequestHandler
    {
        private RequestObject req;
        private StringBuilder respData;

        public SozlukRequestHandlerBase(RequestObject request)
        {
            this.respData = new StringBuilder();
            this.req = request;
        }

        public T GetValue<T>(string key, bool sanitSql=true)
        {
            T val = this.req.GetItem<T>(key);

            if (typeof(T) == typeof(string))
            {
                string sval = (string)(object)val;

                if (sval == null)
                    return default(T); //null

                sval = sval.ToLower();

                if (sanitSql)
                    sval = InputHelper.SanitizeForSQL(sval);

                return (T)(object)sval;
            }

            return val;
        }
        
        public bool HasKey(string key)
        {
            return this.req.HasItem(key);
        }

        public bool PushResponseContent(string content)
        {
            this.respData.Append(content);
            return true;
        }

        public bool PushResponseItem(string item, object value)
        {
            this.respData.AppendFormat("<{0}>{1}</{2}>", item, value.ToString(), item);
            return true;
        }

        public bool PushResponseItem(string item, string value)
        {
            this.respData.AppendFormat("<{0}>{1}</{2}>", item, value, item);
            return true;
        }

        public virtual bool Process()
        {
            return false;
            
        }

        public bool PushBackToBridge()
        {
            bool result;

            if (this.respData.Length > 0)
            {
                result = this.req.Reply(this.respData.ToString());
            }

            return true;
        }

        public void CompleteRequest()
        {
            this.req.Complete();
            this.req = null;

            this.respData.Clear();
            this.respData = null;
        }
    }
}

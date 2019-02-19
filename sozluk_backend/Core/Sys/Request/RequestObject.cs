using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Request
{
    class RequestObject
    {
        private NamedPipeServerStream serverIoPipe;
        private Dictionary<string, string> items;
        
        public RequestObject(NamedPipeServerStream nps, byte[] requestBuffer, int bufferLength)
        {
            this.serverIoPipe = nps;
            BuildRequestObjectAndQueue(requestBuffer, bufferLength);
        }

        private string NormalizeValue(string value)
        {
            
            value = value.Replace("$lt;", "<").
                Replace("$gt;", ">").Replace("$amp;", "&").Replace("$eq;", "=")
                .Replace("$percnt;", "%");

            value = System.Web.HttpUtility.UrlDecode(value, Encoding.GetEncoding("ISO-8859-9"));
            value = System.Web.HttpUtility.HtmlDecode(value).Replace("$plus;","+");
            

            return value;
        }

        private void Parse(string data)
        {
            string key,value, tagEnd;

            int beg=-1, end=0;

            beg = data.IndexOf("<", beg+1);

            while (beg != -1)
            {
                end = data.IndexOf(">", beg);

                if (end == -1)
                    break;

                beg++;

                key = data.Substring(beg, end - beg);

                tagEnd = string.Format("</{0}>", key);

                beg = end + 1;

                end = data.IndexOf(tagEnd, beg);

                if (end == -1)
                    break;

                value = data.Substring(beg, end - beg);

                items.Add(key.ToLower(), NormalizeValue(value));

                beg = data.IndexOf("<", end + 1);
            }

        }

        private void BuildRequestObjectAndQueue(byte[] buffer, int length)
        {
            string str = Encoding.ASCII.GetString(buffer, 0, length);

            items = new Dictionary<string, string>();


            Parse(str);

            if (items.Count > 0)
            {
                this.IsEnqueued = true;
                Log.Info("Request enqueued");

                RequestQueue.Enqueue(this);
            }
        }

        public bool HasItem(string key)
        {
            return items.ContainsKey(key.ToLower());
        }

        public T GetItem<T>(string key)
        {
            object val;

            if (!HasItem(key))
            {
                return default(T);
            }

            val = items[key.ToLower()];

            try
            {
                val = Convert.ChangeType(val, typeof(T));
            }
            catch
            {
                return default(T);
            }

            return (T)val;
        }

        public bool IsEnqueued
        {
            get;
            private set;
        }

        public bool Reply(string data)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            byte[] dataLen = BitConverter.GetBytes((uint)buffer.Length);

            try
            {
                this.serverIoPipe.Write(dataLen, 0, 4);
                this.serverIoPipe.Write(buffer, 0, buffer.Length);
            }
            catch (Exception e)
            {
                Log.Error("Replying request error: {0}", e.Message);
                buffer = null;
                dataLen = null;
                return false;
            }

            buffer = null;
            dataLen = null;

            return true;
        }

        public void DumpKvList()
        {
            foreach (var key in items.Keys)
            {
                Log.Verbose("Key={0}, Value={1}", key, items[key]);
            }
        }

        public void Complete()
        {
            RequestBridge.RegisterRequestReceiver(this.serverIoPipe);
            this.items.Clear();
            this.items = null;
        }
    }
}

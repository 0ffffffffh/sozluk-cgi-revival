using sozluk_backend.Core.Sys.Handlers;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Request
{
    
    enum RequestType
    {
        EntryAdd,
        EntryGet,
        UserGetPass,
        Search,
        AuthSuser
    }

    class RequestQueue
    {
        private static Queue<ISozlukRequestHandler> Requests;
        private static object synchObj;
        private static ManualResetEvent mre;
        private static bool cancelled;

        static RequestQueue()
        {
            cancelled = false;
            mre = new ManualResetEvent(false);
            synchObj = new object();
            Requests = new Queue<ISozlukRequestHandler>();
        }

        private static ISozlukRequestHandler GetHandlerObject(RequestObject request)
        {
            ISozlukRequestHandler handler = null;
            RequestType rType;

            string typeString;

            if (!request.HasItem("RequestType"))
                return null;

            typeString = request.GetItem<string>("RequestType");

            if (!Enum.TryParse<RequestType>(typeString, out rType))
                return null;

            switch (rType)
            {
                case RequestType.EntryAdd:
                    handler = new EntryAddHandler(request);
                    break;
                case RequestType.EntryGet:
                    handler = new EntryGetHandler(request);
                    break;
                case RequestType.Search:
                    handler = new IndexAndSearchHandler(request);
                    break;
                case RequestType.UserGetPass:
                    handler = new GetUserPassHandler(request);
                    break;
                case RequestType.AuthSuser:
                    handler = new AuthenticateSuserHandler(request);
                    break;
            }

            Log.Info("Got an {0} type request", handler.GetType().FullName);

            return handler;

        }

        public static void Shutdown()
        {
            //obtain lock to make sure there
            //is nobody in the enq/deq op

            Log.Info("request queue shutting down");

            lock (synchObj)
            {
                cancelled = true;
                Log.Info("Signalling waiter events to finish");

                mre.Set();
                
                Requests.Clear();
            }

        }

        public static bool Enqueue(RequestObject reqObj)
        {
            ISozlukRequestHandler handler;
            
            if (cancelled)
                return false;

            handler = GetHandlerObject(reqObj);

            if (handler == null)
                return false;

            lock (synchObj)
            {
                Requests.Enqueue(handler);
                mre.Set();
            }

            return true;
        }

        

        public static ISozlukRequestHandler PickRequest()
        {
            ISozlukRequestHandler req = null;

            mre.WaitOne();

            if (cancelled)
                return null;

            lock(synchObj)
            {
                if (Requests.Count > 0)
                    req = Requests.Dequeue();

                if (Requests.Count == 0)
                {
                    mre.Reset();
                }
            }

            return req;
        }
    }
}

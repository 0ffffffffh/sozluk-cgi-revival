using sozluk_backend.Core.Sys.Handlers;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Threading;


namespace sozluk_backend.Core.Sys.Request
{
    class RequestDispatcher
    {
        private static bool _Active = false;
        private static List<Thread> _Dispatchers;

        static RequestDispatcher()
        {
            _Dispatchers = new List<Thread>();
        }

        public static bool CreateDispatchers(int dispatcherCount)
        {
            Thread tworker;

            Log.Info("Creating {0} dispatcher worker", dispatcherCount);

            _Active = true;

            for (int i=0;i<dispatcherCount;i++)
            {
                tworker = new Thread(new ParameterizedThreadStart(DispatchWorker));
                tworker.Start(null);

                _Dispatchers.Add(tworker);
            }

            return true;
        }

        public static void Shutdown()
        {
            _Active = false;

            Log.Info("waiting dispatchers to finish");

            foreach (Thread t in _Dispatchers)
            {
                t.Join();
            }

            Log.Info("all dispatchers finished their jobs");

            _Dispatchers.Clear();
        }

        private static void DispatchWorker(object o)
        {
            ISozlukRequestHandler handler;

            while (_Active)
            {
                handler = RequestQueue.PickRequest();

                if (handler != null)
                {
                    if (handler.Process())
                    {
                        if (handler.PushBackToBridge())
                        {
                            handler.CompleteRequest();
                            handler = null;
                        }
                    }
                }

            }

            Log.Info("Dispatch worker {0} exited", Thread.CurrentThread.ManagedThreadId);

        }
    }
}

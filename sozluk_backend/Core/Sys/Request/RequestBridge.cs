using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using sozluk_backend.Core.Sys.Logging;

namespace sozluk_backend.Core.Sys.Request
{
    static class RequestBridge
    {
        static Thread[] waiters;
        static bool active;

        private static NamedPipeServerStream CreatePipe()
        {
            NamedPipeServerStream nps;

            try
            {
                nps = new NamedPipeServerStream(
                    "sozluk_request_bridge_pipe",
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte
                    );

            }
            catch (Exception e)
            {
                Log.Error("Pipe create error. {0}", e.Message);
                return null;
            }

            Log.Info("Bridge pipe server created");

            return nps;
        }


        private static void RequestWaiter(object o)
        {
            NamedPipeServerStream nps = null;
            
            while (active)
            {
                nps = CreatePipe();

                if (nps == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                try
                {
                    nps.WaitForConnection();

                    if (!active)
                    {
                        nps.Close();
                        nps.Dispose();
                        nps = null;
                    }
                }
                catch (Exception e)
                {
                    Log.Warning("pipe connection wait aborted. {0}", e.Message);
                    nps = null;
                }

                if (nps != null)
                    RegisterRequestReceiver(nps);

            }

            Log.Verbose("Pipe connection waiter Tid#{0} closed.", Thread.CurrentThread.ManagedThreadId);

        }

        private static void WaitIo(object obj)
        {
            uint totalBytes;
            int readLen = 0;
            byte[] buffer = new byte[32 * 1024];
            

            NamedPipeServerStream nps = (NamedPipeServerStream)obj;
            RequestObject reqObj;

            Log.Verbose("Thread#{0} waiting for available incoming data", 
                Thread.CurrentThread.ManagedThreadId);

            if (nps.Read(buffer, 0, 4) > 0)
            {
                totalBytes = BitConverter.ToUInt32(buffer, 0);
                
                readLen = nps.Read(buffer, 0, buffer.Length);
                
                reqObj = new RequestObject(nps, buffer, readLen);

                if (!reqObj.IsEnqueued)
                    reqObj = null;
            }
            else
            {
                nps.Disconnect();
                nps.Close();
                nps.Dispose();
                nps = null;
            }

            buffer = null;
        }

        public static bool RegisterRequestReceiver(NamedPipeServerStream nps)
        {
            return ThreadPool.QueueUserWorkItem(new WaitCallback(WaitIo), nps);
        }

        public static void CreatePipeServers(int waiterCount)
        {
            Log.Verbose("Creating '{0}' pipe(s) connection waiter", waiterCount);

            waiters = new Thread[waiterCount];

            active = true;

            for (int i = 0; i < waiters.Length; i++)
            {
                waiters[i] = new Thread(new ParameterizedThreadStart(RequestWaiter));
                waiters[i].Start(null);
            }
        }

        private static void AbortBlockingWaitOfPipeServer(int count)
        {
            for (int i = 0; i < count; i++)
            {
                NamedPipeClientStream nspc = new NamedPipeClientStream("sozluk_request_bridge_pipe");
                nspc.Connect();

                nspc.Close();
                nspc.Dispose();
            }
        }

        public static void Shutdown()
        {
            active = false;

            Log.Info("Bridge shutting down op signalled");

            AbortBlockingWaitOfPipeServer(waiters.Length);

            foreach (Thread tworker in waiters)
            {
                Log.Verbose("T#{0} Waiting worker to finish",tworker.ManagedThreadId);
                tworker.Join();
            }

            waiters = null;

        }
    }
}

using sozluk_backend.Core.Memcached;
using sozluk_backend.Core.Sys.Request;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using sozluk_backend.Core.Sys.DataStore;
using sozluk_backend.Core.Sys.Model;
using sozluk_backend.Core.Edi;
using sozluk_backend.Core.Sys;

namespace sozluk_backend
{
    class Program
    {
        internal static Memcached DataCacheInstance;
        internal static object DataCacheAccessLock = new object();

        private static InternalTalk Talk;

        private static bool running = true;

        static void Init()
        {
            bool watchdog = !System.Diagnostics.Debugger.IsAttached;
            Log.Warning("Initializing backend. (watchdog? = {0})",watchdog);

            RequestDispatcher.CreateDispatchers(4);
            RequestBridge.CreatePipeServers(4);

            Edi.StartEdi();
            
            //start health monitor.
            System.Diagnostics.Process.Start(
                Config.Get().SbmonPath, 
                string.Format("-backend {0} -memcached {1} -mport {2} -watchdog {3}", 
                System.Diagnostics.Process.GetCurrentProcess().Id,
                DataCacheInstance.ProcessId,Config.Get().MemcachedPort,watchdog));

            LogService.Start();
            Talk = new InternalTalk(true);
            Talk.OnTalk += Talk_OnTalk;
            
        }

        private static void Talk_OnTalk(Dictionary<string, string> v)
        {
            int pid, port;

            if (v["type"] == "AttachMemcached")
            {
                pid = Helper.ConvertTo<int>(v["mpid"]);
                port = Helper.ConvertTo<int>(v["mport"]);

                if (pid > 0 && port > 0)
                {
                    Log.Warning("Attaching the memcached for pid {0}",pid);

                    
                    DataCacheInstance = Memcached.AttachExist("GeneralCache", (ushort)port, pid);
                }
            }
        }

        static void Uninit()
        {
            Log.Warning("Shutting down backend");

            Helper.KillProcess("sbmon");

            Talk.Stop();

            LogService.Stop();

            Edi.KillEdi();

            RequestBridge.Shutdown();

            RequestQueue.Shutdown();

            RequestDispatcher.Shutdown();

            DataCacheInstance.Instance.Shutdown();

            
        }

        static T TryGetOpt<T>(string key,string [] args, T fail)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == key)
                {
                    if (i+1 != args.Length)
                    {
                        try
                        {
                            return (T)Convert.ChangeType(args[i + 1], typeof(T));
                        }
                        catch
                        {
                            return fail;
                        }
                    }
                }
            }

            return fail;
        }

        static void Main(string[] args)
        {
            int mpid;
            ushort mport;

            Log.Init("backend");

            Log.EnableLogType(LogType.Critical);
            Log.EnableLogType(LogType.Verbose);
            Log.EnableLogType(LogType.Info);
            Log.EnableLogType(LogType.Error);
            Log.EnableLogType(LogType.Warning);

            if (!Config.Get().IsOK)
            {
                Log.Critical("Some of required config settings missing.");
                return;
            }

            Log.DisableAll();
            Log.EnableLogType((LogType)Config.Get().LogLevel);

            Log.Info("Booting up memcached instance");

            mpid = TryGetOpt<int>("-mpid", args, 0);
            mport = TryGetOpt<ushort>("-mport", args, 0);

            if (mpid > 0 && mport > 0)
            {
                Log.Warning("Attach requested at pid: {0} and port {1}", mpid, mport);
                DataCacheInstance = Memcached.AttachExist("GeneralCache", mport, mpid);
            }
            else
                DataCacheInstance = Memcached.Create("GeneralCache", 512, 11211);

            if (Program.DataCacheInstance == null)
                Log.Critical("Memcached could not started");
            else
                Log.Info("Memcached ok");



            Init();

            Console.CancelKeyPress += Console_CancelKeyPress;

            while (running)
                Thread.Sleep(10);

            Uninit();

            Console.WriteLine("All resources released. press any key to exit");

            Log._Finalize();

            Console.ReadKey();
            Environment.Exit(0);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            running = false;
        }
    }
}

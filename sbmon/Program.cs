using sozluk_backend.Core.Sys;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace sbmon
{
    static class Program
    {
        public static ushort MEMCACHED_PORT = 0;
        internal static bool running = true;
        static Mutex mutant;
        static Watchdog watchdog;
        internal static InternalTalk talk;

        static T GetVal<T>(Dictionary<string,string> opts, string key, T failIdent)
        {
            string v;
            if (!opts.ContainsKey(key))
            {
                return failIdent;
            }

            v = opts[key];

            try
            {
                return (T)Convert.ChangeType(v, typeof(T));
            }
            catch
            {
                return failIdent;
            }
        }

        static bool InstanceCheck()
        {
            
            if (Mutex.TryOpenExisting("sbmon_instance_mutant", out mutant))
            {
                return false;
            }

            mutant = new Mutex(true, "sbmon_instance_mutant");

            return true;
        }

        [STAThread]
        static void Main(string[] args)
        {
            bool enableWatchdog;
            
            Log.Init("sbmon");
            
            
            Log.EnableAll();
            int backendPid,memcachedPid;
            
            var opts = Helper.ParseOptions(string.Join(" ", args));

            talk = new InternalTalk(false);

            backendPid = GetVal<int>(opts, "-backend", 0);
            memcachedPid = GetVal<int>(opts, "-memcached", 0);
            MEMCACHED_PORT = GetVal<ushort>(opts, "-mport", 0);
            enableWatchdog = GetVal<bool>(opts, "-watchdog", true);
            
            if (backendPid==0 && memcachedPid==0)
            {
                Log.Critical("There are no pids supplied to monitor");
                return;
            }

            if (!InstanceCheck())
            {
                return;
            }

            if (enableWatchdog)
            {
                watchdog = new Watchdog();
                watchdog.Start();
            }
            else
            {
                Log.Warning("Watchdog disabled.");
            }

            if (backendPid > 0)
                new HealthMonitor(backendPid, HealthMonitor.ProcessType.BackendProcess
                    ,RecoverPolicy.RecoverBackendAndPassExistedMemcachedInstance).Start();

            if (memcachedPid > 0)
                new HealthMonitor(memcachedPid, HealthMonitor.ProcessType.MemcachedProcess,
                    RecoverPolicy.RecoverMemcachedAndPassNewMemcachedInstance).Start();

            Console.CancelKeyPress += Console_CancelKeyPress;

            while (running)
                Thread.Sleep(10);

            if (enableWatchdog)
                watchdog.Dispose();

            talk.Stop();

            mutant.Dispose();

            Log._Finalize();

        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            running = false;
            e.Cancel = true;
            
        }
    }
}

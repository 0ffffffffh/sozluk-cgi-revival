using sozluk_backend.Core.Sys;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Memcached
{
    
    class MemcachedProcess
    {

        private static Dictionary<int, MemcachedProcess> _Processes;

        static MemcachedProcess()
        {
            _Processes = new Dictionary<int, MemcachedProcess>();
        }

        private Process process;

        private static void Process_Exited(object sender, EventArgs e)
        {
            MemcachedProcess mp;
            Process process = sender as Process;

            Log.Error("memcached process {0} had lost with {1} code",
                process.Id, process.ExitCode);

            if (_Processes.TryGetValue(process.Id, out mp))
            {
                if (mp.Exited != null)
                {
                    Log.Info("Calling memcached process's exit event");
                    mp.Exited(mp, null);
                }

                Log.Info("removing instance from the mc proc list");

                _Processes.Remove(process.Id);
            }

            //register new instance search
        }

        public static MemcachedProcess Attach(int pid)
        {
            MemcachedProcess mcproc;
            Process nativeProcess;

            nativeProcess = Process.GetProcessById(pid);

            if (nativeProcess==null)
            {
                Log.Critical("Pid {0} could not be found", pid);
                return null;
            }

            nativeProcess.Exited += Process_Exited;
            nativeProcess.EnableRaisingEvents = true;

            mcproc = new MemcachedProcess();
            mcproc.process = nativeProcess;

            if (!_Processes.ContainsKey(mcproc.process.Id))
                _Processes.Add(mcproc.process.Id, mcproc);

            Log.Info("Pid {0} attached successfuly as memcachedprocess", pid);

            return mcproc;
        }


        public static MemcachedProcess FireUp(ulong memSize, ushort port)
        {
            MemcachedProcess mcproc = null;

            string arg;
            ProcessStartInfo psi = null;

            arg = string.Format("-p {0} -m {1}", port,memSize);
            psi = new ProcessStartInfo(Config.Get().MemcachedPath,arg);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;

            
            mcproc = new MemcachedProcess();

            try
            {
                mcproc.process = new Process();
                mcproc.process.EnableRaisingEvents = true;
                mcproc.process.Exited += Process_Exited;
                mcproc.process.StartInfo = psi;

                if (!mcproc.process.Start())
                {
                    mcproc.process = null;
                    mcproc = null;

                    Log.Error("Process could not be started");

                    return null;

                }

            }
            catch (Exception e)
            {
                mcproc = null;
                Log.Error("memcached process start error: %s", e.Message);
                return null;
            }

            if (!_Processes.ContainsKey(mcproc.process.Id))
                _Processes.Add(mcproc.process.Id, mcproc);

            return mcproc;
        }

        public bool Kill()
        {
            this.process.Kill();
            this.process.WaitForExit(5000);

            if (this.process.HasExited)
            {
                if (_Processes.ContainsKey(this.process.Id))
                    _Processes.Remove(this.process.Id);

                return true;
            }

            return false;
        }

        public bool IsOK
        {
            get
            {
                return !this.process.HasExited;
            }
        }

        public object Tag
        {
            get;
            set;
        }

        public int Id
        {
            get
            {
                if (this.process == null)
                    return 0;
                return this.process.Id;
            }
        }

        public event EventHandler Exited;

    }
}

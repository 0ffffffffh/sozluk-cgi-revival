using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using sozluk_backend.Core.Sys.Logging;

namespace sbmon
{

    class HealthMonitor
    {
        public enum ProcessType
        {
            BackendProcess,
            MemcachedProcess
        }

        private static HealthMonitor BackendHealthMon;
        private static HealthMonitor MemcachedHealthMon;
        
        private RecoverPolicy recoverPolicy;
        private ProcessType procType;
        private int pid;
        
        private string processPath, processArgs;
        private Process process;
        

        public HealthMonitor(int pid, ProcessType type, RecoverPolicy policy)
        {
            this.procType = type;
            this.pid = pid;
            this.recoverPolicy = policy;
        }

        private string RebuildArgList(string argList, string extras)
        {
            if (string.IsNullOrEmpty(extras))
                return argList;

            StringBuilder sb = new StringBuilder();
            string s;
            Dictionary<string, string> eArgs = Helper.ParseOptions(argList);
            Dictionary<string, string> extraDict = Helper.ParseOptions(extras);

            foreach (var key in extraDict.Keys)
            {
                if (eArgs.ContainsKey(key))
                {
                    eArgs[key] = extraDict[key];
                }
                else
                {
                    eArgs.Add(key, extraDict[key]);
                }
            }


            extraDict.Clear();
            extraDict = null;

            foreach (var key in eArgs.Keys)
            {
                sb.AppendFormat("{0} {1} ", key, eArgs[key]);
            }

            s = sb.ToString().TrimEnd();

            eArgs.Clear();
            eArgs = null;

            sb.Clear();
            sb = null;

            return s;
        }

        private bool RestartProcessWithExistedInfo(string extraArgs)
        {
            Log.Info("Restaring process {0} with extra args {1}", procType, extraArgs == null ? "None" : extraArgs);

            string newArgList = RebuildArgList(this.processArgs, extraArgs);

            ProcessStartInfo psi = new ProcessStartInfo(this.processPath, newArgList);
            psi.UseShellExecute = true;

            try
            {
                this.process = Process.Start(psi);
            }
            catch (Exception e)
            {
                Log.Critical("the process could not be recovered." + e.Message);
                return false;
            }


            this.process.Exited += Process_Exited;
            this.process.EnableRaisingEvents = true;
            this.processArgs = this.process.StartInfo.Arguments;
            this.pid = this.process.Id;

            Log.Info("New process created successfuly Pid: {0}", pid);

            return true;
        }
        
        private bool RecoverBackendAndPassWorkingMemcachedInstance()
        {
            string passingArg;

            if (this.procType != ProcessType.BackendProcess)
            {
                return false;
            }

            passingArg = string.Format("-mpid {0}", MemcachedHealthMon.pid);

            
            if (Program.MEMCACHED_PORT > 0)
                passingArg += string.Format(" -mport {0}", Program.MEMCACHED_PORT);


            return RestartProcessWithExistedInfo(passingArg);
        }

        private bool RecoverMemcachedAndPassNewMemcachedToBackend()
        {
            if (this.procType != ProcessType.MemcachedProcess)
                return false;

            if (!RestartProcessWithExistedInfo(null))
                return false;

            Log.Info("Created new memcached instance");

            var data = Program.talk.CreateTalkData();
            
            data.Add("Type", "AttachMemcached");
            data.Add("mpid", pid.ToString());
            data.Add("mport", "11211");

            Log.Info("Sending to the backend pid: {0}", pid);

            Program.talk.SendTalkData(data);
            Program.talk.DisposeTalkData(ref data);

            return true;
        }


        private bool RecoverMemcached()
        {
            if (this.procType != ProcessType.MemcachedProcess)
                return false;

            return RestartProcessWithExistedInfo(null);
        }

        private bool ShutdownBackendOnMemcachedCrash()
        {
            if (this.procType != ProcessType.MemcachedProcess)
                return false;

            Log.Warning("Shutting down the backend...");

            BackendHealthMon.Leave(true);
            BackendHealthMon = null;

            return true;
        }

        private bool ShutdownMemcachedAndRestartBackendOnBackendCrash()
        {
            if (this.procType != ProcessType.BackendProcess)
                return false;

            Log.Warning("Shutting down the memcached...");

            MemcachedHealthMon.Leave(true);
            MemcachedHealthMon = null;

            return RestartProcessWithExistedInfo(null);
        }

        private bool ShutdownMemcachedOnBackendCrash()
        {
            if (this.procType != ProcessType.BackendProcess)
                return false;

            Log.Warning("Shutting down the memcached...");

            MemcachedHealthMon.Leave(true);
            MemcachedHealthMon = null;

            return true;
        }

        private bool ApplyRecoverPolicy()
        {
            Log.Warning("Applying recover policy {0} for {1}", recoverPolicy, procType);

            switch (recoverPolicy)
            {
                case RecoverPolicy.RecoverBackendAndPassExistedMemcachedInstance:
                    return RecoverBackendAndPassWorkingMemcachedInstance();
                case RecoverPolicy.RecoverMemcachedAndPassNewMemcachedInstance:
                    return RecoverMemcachedAndPassNewMemcachedToBackend();
                case RecoverPolicy.RecoverMemcachedOnly:
                    return RecoverMemcached();
                case RecoverPolicy.ShutdownBackendIfMemcachedCrashed:
                    return ShutdownBackendOnMemcachedCrash();
                case RecoverPolicy.ShutdownMemcachedAndRecoverBackendIfBackendCrashed:
                    return ShutdownMemcachedAndRestartBackendOnBackendCrash();
                case RecoverPolicy.ShutdownMemcachedIfBackendCrashed:
                    return ShutdownMemcachedOnBackendCrash();
            }

            return false;
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            
            if (this.process.ExitCode != 0)
            {
                Log.Warning("{0} ({1}) has exited abnormally. applying recover policy",
                    procType, pid);

                Leave();
                ApplyRecoverPolicy();
                // looks like the process crashed.
            }
        }

        public bool Start()
        {
            this.process = Process.GetProcessById(this.pid);

            if (this.process == null)
            {
                Log.Error("Pid: {0} could not be found.", pid);
                return false;
            }

            this.processPath = this.process.MainModule.FileName;
            this.processArgs = this.process.StartInfo.Arguments;
            
            Log.Verbose(this.processArgs);

            this.process.Exited += Process_Exited;
            this.process.EnableRaisingEvents = true;

            switch (this.procType)
            {
                case ProcessType.BackendProcess:
                    BackendHealthMon = this;
                    break;
                case ProcessType.MemcachedProcess:
                    MemcachedHealthMon = this;
                    break;
            }

            Log.Info("Monitoring started on Pid: {0} for the type of {1} process", this.pid, this.procType);
            
            return true;
        }

        private void Leave(bool kill)
        {
            if (this.process == null)
                return;

            Log.Info("Leaving monitoring from {0} ({1})", procType, pid);

            this.process.EnableRaisingEvents = false;
            this.process.Exited -= Process_Exited;
            

            if (kill)
            {
                Log.Warning("Kill requested...");
                this.process.Kill();
                this.process.WaitForExit(1000);
            }

            this.process = null;
        }

        public void Leave()
        {
            Leave(false);
        }
    }
}

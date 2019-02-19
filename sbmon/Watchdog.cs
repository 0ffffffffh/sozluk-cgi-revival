using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sbmon
{
    class Watchdog
    {
        class WatchdogTimerState
        {
            private WatchdogState ws;

            public WatchdogTimerState(WatchdogState ws)
            {
                this.ws = ws;
            }

            public WatchdogState Ws
            {
                get
                {
                    return ws;
                }
            }

            public WatchdogState Change(WatchdogState newWs)
            {
                return Interlocked.Exchange<WatchdogState>(ref ws, newWs);
            }
        }

        class WatchdogState
        {
            public static WatchdogState Create()
            {
                WatchdogState ws = new WatchdogState();
                ws.FirstSnapshot = new List<Process>();
                ws.SecondSnapshot = new List<Process>();

                return ws;
            }

            public void Release()
            {
                FirstSnapshot.Clear();
                SecondSnapshot.Clear();

                FirstSnapshot = null;
                SecondSnapshot = null;
            }

            public List<Process> FirstSnapshot;
            public List<Process> SecondSnapshot;

            public bool IsFirstTaken
            {
                get;
                set;
            }
        }

        private Timer workTimer = null;
        private WatchdogTimerState wts = null;
        private HashSet<string> watchList;
        private WaitCallback watchdogWorkerCb = null;
        
        private bool IsWatchableProcess(Process proc)
        {
            return watchList.Contains(proc.ProcessName.ToLower());
        }

        private IEnumerable<Process> Intersect(WatchdogState ws)
        {
            if (ws.FirstSnapshot.Count == 0)
                yield break;
            
            foreach (var fsp in ws.FirstSnapshot)
            {
                foreach (var ssp in ws.SecondSnapshot)
                {
                    if (fsp.ProcessName.ToLower() == ssp.ProcessName.ToLower())
                    {
                        yield return fsp;
                        break;
                    }
                }
            }
        }

        private void KillList(IEnumerable<Process> processes)
        {
            foreach (var proc in processes)
            {
                Log.Info("Attempting to kill pid {0}", proc.Id);

                try
                {
                    proc.Kill();
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                    proc.Dispose();
                    continue;
                }

                if (!proc.WaitForExit(2000))
                {
                    Log.Error("Pid: {0} cant be killed!", proc.Id);
                }

                proc.Dispose();
            }
        }

        private void WatchdogWorkerRoutine(object obj)
        {
            WatchdogState ws = (WatchdogState)obj;
            IEnumerable<Process> stillWorkingList;

            if (ws.IsFirstTaken)
            {
                SnapshotProcesses(ws.SecondSnapshot);

                stillWorkingList = Intersect(ws);

                if (stillWorkingList != null)
                {
                    Log.Warning("There is {0} process(es) being hung",stillWorkingList.Count());
                    KillList(stillWorkingList);
                }
                else
                    Log.Info("There is no hang process.");

                ws.Release();
                ws = null;
            }
        }

        private void SnapshotProcesses(List<Process> processes)
        {
            Process[] procs = Process.GetProcesses();
            processes.Clear();

            foreach (var proc in procs)
            {
                if (IsWatchableProcess(proc))
                    processes.Add(proc);
            }

            procs = null;
        }

        private bool Recalibrate(int ms)
        {
            return workTimer.Change(ms, ms);
        }

        private void Check(object obj)
        {
            WatchdogTimerState wts = (WatchdogTimerState)obj;

            if (!wts.Ws.IsFirstTaken)
            {
                
                SnapshotProcesses(wts.Ws.FirstSnapshot);

                if (wts.Ws.FirstSnapshot.Count > 0)
                {
                    wts.Ws.IsFirstTaken = true;
                    Recalibrate(5 * 1000);
                }

            }
            else
            {
                WatchdogState prevWs = wts.Ws;
                wts.Change(WatchdogState.Create());

                ThreadPool.QueueUserWorkItem(watchdogWorkerCb,prevWs);
                Recalibrate(30 * 1000);
            }
        }

        public void Start()
        {
            Log.Info("Watchdog starting...");

            WatchdogState ws = WatchdogState.Create();
            
            watchList = new HashSet<string>();
            watchdogWorkerCb = new WaitCallback(WatchdogWorkerRoutine);

            
            watchList.Add("view");
            watchList.Add("index");
            watchList.Add("add");
            

            WatchdogTimerState wts = new WatchdogTimerState(ws);
            this.wts = wts;

            workTimer =
                new Timer(new TimerCallback(Check), wts, 10 * 1000, 30 * 1000);

            
            Log.Info("Watchdog started");
        }

        public void Dispose()
        {
            if (workTimer == null)
                return;

            Log.Info("Watchdog stopping..");

            ManualResetEvent mre = new ManualResetEvent(false);
            
            workTimer.Dispose(mre);

            mre.WaitOne();

            if (this.wts.Ws != null)
                this.wts.Ws.Release();

            watchList.Clear();
            watchList = null;

            watchdogWorkerCb = null;

            mre.Dispose();

            Log.Info("Watchdog stopped");
            

        }

    }
}

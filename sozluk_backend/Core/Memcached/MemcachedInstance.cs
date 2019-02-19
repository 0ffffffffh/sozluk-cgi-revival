using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Memcached
{
    class MemcachedInstance
    {
        private static Hashtable _MemcachedInstances = null;

        private static object _InstanceListLock;

        static MemcachedInstance()
        {
            _MemcachedInstances = new Hashtable();
            _InstanceListLock = new object();
        }

        private ulong memSize;
        private string name;
        private ushort port;

        private MemcachedProcess process;

        public static MemcachedInstance FindInstance(string name)
        {
            name = name.ToLower();

            if (_MemcachedInstances.ContainsKey(name))
            {
                return _MemcachedInstances[name] as MemcachedInstance;
            }

            return null;
        }

        private bool IsInstanceExists(string instanceName)
        {
            return _MemcachedInstances.ContainsKey(instanceName.ToLower());
        }

        private MemcachedInstance GetInstance(string name)
        {
            if (_MemcachedInstances.ContainsKey(name))
            {
                return _MemcachedInstances[name] as MemcachedInstance;
            }

            return null;
        }

        private bool PutInstance(MemcachedInstance instance, string name)
        {
            lock (_InstanceListLock)
            {
                if (IsInstanceExists(name))
                    return false;

                _MemcachedInstances.Add(name.ToLower(), instance);
            }

            return true;
        }

        private void RemoveInstance(string name)
        {
            lock (_InstanceListLock)
            {
                if (IsInstanceExists(name))
                {
                    _MemcachedInstances.Remove(name.ToLower());
                }
            }
        }

        public static MemcachedInstance CreateInstanceFromExist(int pid, ushort port, string name)
        {
            MemcachedInstance inst = new MemcachedInstance(0, name, port);

            if (!inst.PutInstance(inst, inst.name))
                throw new Exception(string.Format("{0} is already registered!", inst.name));

            inst.process = MemcachedProcess.Attach(pid);

            if (inst.process == null)
            {
                inst.RemoveInstance(name);
                inst = null;
                return null;
            }

            inst.process.Tag = name;
            inst.process.Exited += inst.Process_Exited;

            return inst;
        }

        public static bool WaitUntilInstanceDestroyed(string name, TimeSpan ts)
        {
            DateTime breakTime = DateTime.Now.AddTicks(ts.Ticks);

            while (FindInstance(name) != null)
            {
                if (DateTime.Now >= breakTime)
                    return false;

                Thread.Sleep(100);
            }

            return true;
        }

        public MemcachedInstance(ulong memorySize, string instanceName,ushort port)
        {
            this.memSize = memorySize;
            this.port = port;
            this.name = instanceName.ToLower();
        }

        public bool Start()
        {
            if (!PutInstance(this, this.name))
                throw new Exception(string.Format("{0} is already registered!", this.name));

            process = MemcachedProcess.FireUp(this.memSize, this.port);

            if (process == null)
            {
                RemoveInstance(this.name);
                return false;
            }

            process.Tag = this.name;
            process.Exited += Process_Exited;

            return true;
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            MemcachedProcess process = sender as MemcachedProcess;
            string instanceName;

            
            instanceName = process.Tag as string;
            RemoveInstance(instanceName);

        }

        public bool Kill()
        {
            return this.process.Kill();
        }

        public int ProcessId
        {
            get
            {
                return this.process.Id;
            }
        }
        public bool IsRunning
        {
            get
            {
                return this.process.IsOK;
            }
        }

        public ushort Port
        {
            get
            {
                return this.port;
            }
        }
    }
}

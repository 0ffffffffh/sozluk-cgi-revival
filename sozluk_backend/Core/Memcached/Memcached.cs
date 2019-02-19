using sozluk_backend.Core.Sys;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Memcached
{
    class Memcached
    {
        private MemcachedInstance inst;
        
        public static Memcached AttachExist(string name, ushort port, int pid)
        {
            Memcached memcached = new Memcached();
            MemcachedIo mio;

            Log.Info("Attaching exist memcached instance.");

            if (!MemcachedInstance.WaitUntilInstanceDestroyed(name,new TimeSpan(0,0,5)))
            {
                Log.Warning("{0} could not be deregistered", name);
                return null;
            }

            try
            {
                memcached.inst = MemcachedInstance.CreateInstanceFromExist(pid, port, name);
            }
            catch (Exception e)
            {
                //probably still exist
                Log.Warning(e.Message);
                return null;
            }

            Log.Info("instance \"{0}\" created at {1} port", name, port);


            Log.Info("attaching to the memcached IO interface.");

            mio = new MemcachedIo();

            if (!mio.AttachMemcachedInstance(memcached.inst))
            {
                Log.Error("Memcached instance could not be attached to Memcached Io object");
                memcached.inst.Kill();
                memcached.inst = null;
                memcached = null;

                return null;
            }

            memcached.Instance = mio;

            Log.Info("Connection success...");

            return memcached;
        }

        public static Memcached Create(string name, ulong memSize, ushort port)
        {
            Memcached memcached = new Memcached();
            MemcachedIo mio;

            Log.Info("Creating new memcached instance.");

            memcached.inst = new MemcachedInstance(memSize, name, port);


            if (!memcached.inst.Start())
            {
                Log.Critical("Memcached instance could not be created!!");
                memcached.inst = null;
                memcached = null;

                return null;
            }

            Log.Info("instance \"{0}\" created at {1} port", name, port);


            Log.Info("attaching and connecting to the instance.");

            mio = new MemcachedIo();
            
            if (!mio.AttachMemcachedInstance(memcached.inst))
            {
                Log.Error("Memcached instance could not be attached to Memcached Io object");
                memcached.inst.Kill();
                memcached.inst = null;
                memcached = null;

                return null;
            }

            memcached.Instance = mio;

            Log.Info("Connection success...");

            return memcached;
        }

        public MemcachedIo Instance
        {
            get;
            private set;
        }

        public int ProcessId
        {
            get
            {
                return Instance.ProcessId;
            }
        }
    }
}

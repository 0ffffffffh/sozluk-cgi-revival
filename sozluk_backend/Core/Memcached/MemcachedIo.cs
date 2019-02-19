using System;
using System.Text;
using System.Net;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using sozluk_backend.Core.Sys.Logging;

namespace sozluk_backend.Core.Memcached
{
    class MemcachedIo
    {
        private MemcachedClient     mc;
        private IPEndPoint          serverIp;
        private MemcachedInstance   instance;
        
        internal int ProcessId
        {
            get
            {
                return instance.ProcessId;
            }
        }

        public bool AttachMemcachedInstance(MemcachedInstance instance)
        {
            if (!instance.IsRunning)
                return false;

            MemcachedClientConfiguration conf = new MemcachedClientConfiguration();

            this.serverIp = new IPEndPoint(IPAddress.Parse("127.0.0.1"), instance.Port);

            conf.Protocol = MemcachedProtocol.Binary;
            conf.Servers.Add(this.serverIp);

            this.mc = new MemcachedClient(conf);

            this.instance = instance;

            return true;
        }

        public void Flush()
        {
            this.mc.FlushAll();
        }

        public bool Shutdown()
        {
            Flush();

            this.mc.Dispose();

            return this.instance.Kill();
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return this.mc.Remove(key);
        }

        public bool Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return this.mc.Store(StoreMode.Set, key, value);
        }

        public bool Set(string key, object value, TimeSpan validFor)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return this.mc.Store(StoreMode.Set, key, value, validFor);
        }


        public T Get<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                return default(T);

            return this.mc.Get<T>(key);
        }

        public bool TryGet<T>(string key, out T value)
        {
            bool got;
            object obj;

            value = default(T);

            if (string.IsNullOrEmpty(key))
                return false;

            got = this.mc.TryGet(key, out obj);

            if (!got)
                return false;
            
            value = (T)obj;

            return true;
        }
    }
}

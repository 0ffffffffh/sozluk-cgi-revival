using sozluk_backend.Core.Sys;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace sozluk_backend.Core.Cache
{

    internal class KeysetId
    {
        
        private KeysetId(string keyId, bool discardInvalidKeys)
        {
            ValidateKeys = discardInvalidKeys;
            SetId = keyId;
        }

        public static KeysetId Index(char indexerChr, bool validateKeys = false)
        {
            return new KeysetId("CKS_IND_" + indexerChr.ToString(), validateKeys);
        }


        public static KeysetId Baslik(int baslikId, bool validateKeys = false)
        {
            return new KeysetId("CKS_BSL_" + baslikId.ToString(), validateKeys);
        }

        public static KeysetId Todays(bool validateKeys = false)
        {
            return new KeysetId("CKS_TDY",validateKeys);
        }

        public static KeysetId Search(string searchIdHash, bool validateKeys = false)
        {
            return new KeysetId("CKS_SRCH_" + searchIdHash,validateKeys);
        }

        public string SetId
        {
            get;
            private set;
        }

        public bool ValidateKeys
        {
            get;
            private set;
        }
    }

    static class CacheSet
    {
        internal class CacheKeySetContext
        {
            [NonSerialized]
            public object lck = new object();

            public string CacheSetKey;
        }

        private static object setLock;
        private static Dictionary<string, CacheKeySetContext> CacheSetKeys;

        static CacheSet()
        {
            CacheSetKeys = new Dictionary<string, CacheKeySetContext>();
            setLock = new object();
        }

        internal static void RemoveKeysetContext(KeysetId setId)
        {
            lock (setLock)
            {
                if (CacheSetKeys.ContainsKey(setId.SetId))
                    CacheSetKeys.Remove(setId.SetId);
            }
        }

        internal static CacheKeySetContext GetKeysetContext(KeysetId setId, bool createIfNotExists)
        {
            CacheKeySetContext cskc = null;

            lock(setLock)
            {
                if (!CacheSetKeys.TryGetValue(setId.SetId, out cskc))
                {
                    if (createIfNotExists)
                    {
                        cskc = new CacheKeySetContext();
                        cskc.CacheSetKey =
                            "CS_" + Helper.Md5(Config.Get().CacheSetSalt + setId.SetId);

                        CacheSetKeys.Add(setId.SetId, cskc);
                    }
                }
            }

            return cskc;
        }

        internal static CacheKeySetContext GetKeysetContext(KeysetId setId)
        {
            return GetKeysetContext(setId, true);
        }

        private static void CheckSetKeys(HashSet<string> set)
        {
            var keys = set.ToArray();

            foreach (var key in keys)
            {
                if (!CacheManager.TryGetCachedResult<object>(key,out object val))
                {
                    set.Remove(key);
                }
            }
        }

        internal static bool AddKey(KeysetId setId, string key)
        {
            CacheKeySetContext cskc;
            HashSet<string> set;
            
            cskc = GetKeysetContext(setId);

            if (!CacheManager.TryGetCachedResult<HashSet<string>>(cskc.CacheSetKey,out set))
            {
                set = new HashSet<string>();
                set.Add(key);
                CacheManager.CacheObject(cskc.CacheSetKey, set);
            }
            else
            {
                lock (cskc.lck)
                {
                    if (setId.ValidateKeys)
                        CheckSetKeys(set);

                    if (!set.Contains(key))
                        set.Add(key);
                }

                CacheManager.CacheObject(cskc.CacheSetKey, set);
            }

            return true;
        }

        internal static bool Invalidate(KeysetId setId)
        {
            HashSet<string> sets;
            
            CacheKeySetContext cksc = GetKeysetContext(setId, false);

            if (cksc == null)
                return false;
            
            lock (cksc.lck)
            {
                if (!CacheManager.TryGetCachedResult<HashSet<string>>(cksc.CacheSetKey, out sets))
                {
                    Log.Warning("Not expected situation. cache key set does not exists for setId:{0}", setId);
                    RemoveKeysetContext(setId);
                    return false;
                }

                foreach (var key in sets)
                {
                    CacheManager.Remove(key);
                }

                Log.Info("{0} key records invalidated from the keyset ({1})", sets.Count, setId);

                sets.Clear();

                CacheManager.CacheObject(cksc.CacheSetKey, sets);
            }


            return true;
        }

    }

    static class CacheManager
    {
        internal static string CalculateCacheKey(string v)
        {
            return Helper.Md5(v);
        }

        public static bool Remove(string key)
        {
            return Program.DataCacheInstance.Instance.Remove(key);
        }

        public static bool CacheObject(KeysetId setId, string key, object obj, TimeSpan validFor)
        {
            bool result;

            if (setId != null)
            {
                if (!CacheSet.AddKey(setId, key))
                    Log.Warning("cacheKeyset for {0} could not be added. (key={1})", setId, key);
            }

            if (validFor == TimeSpan.MinValue)
                result = Program.DataCacheInstance.Instance.Set(key, obj);
            else
                result = Program.DataCacheInstance.Instance.Set(key, obj, validFor);

            if (!result)
            {
                Log.Warning("obj({0}) could not be cached for key:{1}", obj.GetType(), key);
                return false;
            }
            
            return true;
        }

        public static bool CacheObject(KeysetId setId, string key, object obj)
        {
            return CacheObject(setId, key, obj, TimeSpan.MinValue);
        }

        public static bool CacheObject(string key, object obj, TimeSpan validFor)
        {
            return CacheObject(null, key, obj, validFor);
        }

        public static bool CacheObject(string key, object obj)
        {
            return CacheObject(key, obj, TimeSpan.MinValue);
        }

        public static bool CacheObject(KeysetId setId, bool hashKeyDataBeforeCache, string cacheKeyData, object result, TimeSpan validFor)
        {
            string key;

            if (hashKeyDataBeforeCache)
                key = CalculateCacheKey(cacheKeyData);
            else
                key = cacheKeyData;

            return CacheObject(setId,key, result, validFor);
        }

        public static bool CacheObject(bool hashKeyDataBeforeCache, string cacheKeyData, object result,TimeSpan validFor)
        {
            return CacheObject(null, hashKeyDataBeforeCache, cacheKeyData, result, validFor);
        }

        public static bool CacheObject(KeysetId setId, bool hashKeyDataBeforeCache, string cacheKeyData, object result)
        {
            return CacheObject(setId, hashKeyDataBeforeCache, cacheKeyData, result,TimeSpan.MinValue);
        }

        public static bool CacheObject(bool hashKeyDataBeforeCache, string cacheKeyData, object result)
        {
            return CacheObject(hashKeyDataBeforeCache, cacheKeyData, result, TimeSpan.MinValue);
        }

        public static bool CreateKeysetIfNotExists(KeysetId setId)
        {
            if (CacheSet.GetKeysetContext(setId, false)==null)
            {
                CacheSet.GetKeysetContext(setId, true);
                return true;
            }

            return false;
        }

        public static string[] GetKeysFromKeySet(KeysetId setId)
        {
            HashSet<string> set;

            var cksc = CacheSet.GetKeysetContext(setId,false);

            if (cksc == null)
                return null;

            TryGetCachedResult<HashSet<string>>(cksc.CacheSetKey, out set);

            if (set == null)
                return null;

            return set.ToArray();
        }

        public static bool TryGetCachedQueryResult<T>(string query, out T value)
        {
            return Program.DataCacheInstance.Instance.TryGet<T>(
                CalculateCacheKey(query),
                out value);
        }

        public static bool TryGetCachedResult<T>(string key, out T value)
        {
            return Program.DataCacheInstance.Instance.TryGet<T>(
                key,
                out value);
        }

        public static bool InvalidateCacheSet(KeysetId setId)
        {
            return CacheSet.Invalidate(setId);
        }

        public static bool Invalidate(string key)
        {
            return Remove(key);
        }
    }
}

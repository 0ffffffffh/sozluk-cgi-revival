using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys
{
    class Config
    {


        private static Config conf = null;

        private static T TryConv<T>(string v, T def)
        {
            try
            {
                return (T)Convert.ChangeType(v, typeof(T));
            }
            catch
            {
                return def;
            }
        }

        public static Config Get()
        {
            string key, val;
            string[] optLines;

            string workDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (!File.Exists(workDir + "\\.config"))
                return conf;

            if (conf != null)
                return conf;


            conf = new Config();

            optLines = File.ReadAllLines(workDir + "\\.config");

            if (optLines == null)
                return conf;

            foreach (var item in optLines)
            {
                var part = item.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                if (part != null && part.Length == 2)
                {
                    key = part[0].Trim();
                    val = part[1].Trim();

                    switch (key)
                    {
                        case "memcached_port":
                            conf.MemcachedPort = TryConv<int>(val, 11211);
                            break;
                        case "memcached_path":
                            conf.MemcachedPath = val;
                            break;
                        case "sbmon_path":
                            conf.SbmonPath = val;
                            break;
                        case "log_level":
                            conf.LogLevel = TryConv<int>(val, (int)LogType.Critical);
                            break;
                        case "dbpwd":
                            conf.DbPassword = val;
                            break;
                        case "test_mode":
                            conf.TestMode = TryConv<bool>(val, true);
                            break;
                        case "html_asset_root":
                            conf.HtmlContentRoot = val;
                            break;
                        case "cacheset_salt":
                            conf.CacheSetSalt = val;
                            break;
                        case "records_per_page":
                            conf.RecordCountPerPage = TryConv<int>(val, 40);
                            break;
                        case "basliks_per_page":
                            conf.BaslikCountPerPage = TryConv<int>(val, 15);
                            break;
                    }
                }
            }
            
            optLines = null;

            return conf;
        }


        public string MemcachedPath
        {
            get;
            private set;
        }

        public int MemcachedPort
        {
            get;
            private set;
        }

        public int LogLevel
        {
            get;
            private set;
        }

        public string SbmonPath
        {
            get;
            private set;
        }

        public string DbPassword
        {
            get;
            private set;
        }

        public bool TestMode
        {
            get;
            private set;
        }

        public string HtmlContentRoot
        {
            get;
            private set;
        }

        public int RecordCountPerPage
        {
            get;
            private set;
        }

        public int BaslikCountPerPage
        {
            get;
            private set;
        }

        public bool IsOK
        {
            get
            {
                if (string.IsNullOrEmpty(MemcachedPath))
                {
                    Log.Critical("memcached path not set");
                    return false;
                }

                if (!File.Exists(MemcachedPath))
                {
                    Log.Critical("memcached binary not found at its path");
                    return false;
                }

                if (string.IsNullOrEmpty(SbmonPath))
                {
                    Log.Critical("sbmon path not set");
                    return false;
                }

                if (!File.Exists(SbmonPath))
                {
                    Log.Critical("sbmon binary not found at its path");
                    return false;
                }

                if (string.IsNullOrEmpty(HtmlContentRoot))
                {
                    Log.Critical("html assets root path not set");
                    return false;
                }
                
                if (Directory.GetFiles(HtmlContentRoot).Length==0)
                {
                    Log.Warning("There is no files in html assets directory");
                }

                if (RecordCountPerPage == 0)
                    RecordCountPerPage = 40;

                if (CacheSetSalt == null)
                    return false;

                return true;
            }
        }

        
        public string CacheSetSalt
        {
            get;
            private set;
        }
    }
}

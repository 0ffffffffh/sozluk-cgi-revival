using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys
{
    static class InputHelper
    {

        public static string SanitizeForXSS(string s)
        {
            return
                s.Replace("&", "&amp;").
                    Replace("<", "&lt;").
                    Replace(">", "&gt;").
                    Replace("\"", "&quot;").
                    Replace("%", "&percnt;").
                    Replace("'", "&#x27;").
                    Replace("/", "&#x2F;");
        }

        public static string SanitizeForSQL(string s)
        {
            return s.Replace("'", "''");
        }

        public static string NormalizeTurkishChars(string text)
        {
            byte[] tempBytes;
            tempBytes = Encoding.GetEncoding("ISO-8859-8").GetBytes(text);
            return Encoding.UTF8.GetString(tempBytes);
        }

    }

    class ExecPerf
    {
        private Stopwatch sw;

        public ExecPerf()
        {
            this.sw = new Stopwatch();
            sw.Start();
        }

        
        public void Begin()
        {
            sw.Start();
        }

        public void Time(string msg)
        {
            sw.Stop();
            Log.Info(msg + " took " + sw.Elapsed.ToString());
        }

        public void Time(string msg, TimeSpan warningThreshold)
        {
            Time(msg);

            if (sw.Elapsed >= warningThreshold)
            {
                Log.Warning("The execution was tooking longer than expected! Threshold: {0}", warningThreshold);
            }
        }
    }

    static class Helper
    {

        private static string HashWith(HashAlgorithm algo,string v)
        {
            string hash;
            StringBuilder sb = new StringBuilder();
            byte[] bytes = Encoding.ASCII.GetBytes(v);

            bytes = algo.ComputeHash(bytes);
            algo.Dispose();

            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("X2"));

            hash = sb.ToString();
            sb.Clear();
            sb = null;
            
            return hash;
        }

        public static string Sha512(string s)
        {
            return HashWith(SHA512.Create(), s);
        }

        public static string Md5(string v)
        {
            return HashWith(MD5.Create(),v);
        }

        public static string GetStatusString(bool status, string success, string fail)
        {
            return status == true ? success : fail;
        }

        public static T ConvertTo<T>(string sval, T defVal = default(T))
        {
            try
            {
                return (T)Convert.ChangeType(sval, typeof(T));
            }
            catch
            {
                return defVal;
            }
        }

        public static void KillProcess(string name)
        {
            Process[] proc = Process.GetProcessesByName(name);

            foreach (var p in proc)
            {
                try
                {
                    p.Kill();
                }
                catch (Exception e)
                {
                    Log.Verbose(e.Message);
                }
            }
        }
    }
}

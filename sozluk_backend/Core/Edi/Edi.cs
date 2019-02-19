using sozluk_backend.Core.Sys.Logging;
using System;
using System.Text;
using System.Threading;

namespace sozluk_backend.Core.Edi
{
    class Edi
    {
        static EdisFace ediSvc;
        static Timer wipeTimer;
        static string sessionId;
        static bool wipeDelayed = false;

        internal static string MakeUniqueCacheKey(string desc)
        {
            return desc + "_" + sessionId;
        }

        static void DailyClientInfoWiper(object state)
        {
            if (!ediSvc.WipeClientInfos())
            {
                wipeDelayed = true;

                wipeTimer.Change(
                    (int)TimeSpan.FromMinutes(5).TotalMilliseconds,
                    (int)TimeSpan.FromMinutes(5).TotalMilliseconds
                    );

                Log.Warning("Client state wipe cant be done. Will be trying in 5 minutes again");
            }
            else
            {
                if (wipeDelayed)
                {
                    wipeTimer.Change(GetOccurancePeriodInterval(), (int)TimeSpan.FromDays(1).TotalMilliseconds);
                    wipeDelayed = false;
                }

                Log.Info("Client state list wipe successful.");
            }
        }

        private static void GenSessionId()
        {
            StringBuilder sb = new StringBuilder();
            string hx = "abcdefgh0123456789";
            Random rnd = new Random();

            for (int i=0;i<16;i++)
            {
                sb.Append(hx[rnd.Next(hx.Length)]);
            }

            sessionId = sb.ToString();
            sb.Clear();
            sb = null;
        }

        private static int GetOccurancePeriodInterval()
        {
            var ts = DateTime.Today.AddDays(1) - DateTime.Now;
            return (int)ts.TotalMilliseconds;
        }

        public static bool StartEdi()
        {
            Log.Info("Edi registration handler starting at port 1999");

            GenSessionId();

            ediSvc = new EdisFace(1999);

            if (!ediSvc.IsAlive)
            {
                ediSvc = null;
                return false;
            }

            
            wipeTimer = new Timer(
                new TimerCallback(DailyClientInfoWiper), ediSvc, GetOccurancePeriodInterval(), 
                (int)TimeSpan.FromDays(1).TotalMilliseconds);

            Log.Info("Reginfo housekeeper starting..");

            return true;
        }

        public static bool KillEdi()
        {
            if (ediSvc == null)
                return true;

            ediSvc.WipeClientInfos();

            wipeTimer.Dispose();
            wipeTimer = null;
            ediSvc.Close();
            ediSvc = null;

            return true;
        }
    }
}

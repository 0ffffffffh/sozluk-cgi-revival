using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Logging
{
    enum LogType
    {
        Verbose = 1,
        Info = 2,
        Warning = 4,
        Error = 8,
        Critical = 16
    }

    class Log
    {
        private static object consLock = new object();

        private static ulong logMask = 0;

        private static LogFileWriter logFile;

        
        public static void Init(string module)
        {
            logFile = new LogFileWriter(module);
        }

        public static void _Finalize()
        {
            logFile.Dispose();
        }
        
        private static ConsoleColor GetColorByLogType(LogType type)
        {
            switch (type)
            {
                case LogType.Verbose:
                    return ConsoleColor.White;
                case LogType.Info:
                    return ConsoleColor.Blue;
                case LogType.Warning:
                    return ConsoleColor.Yellow;
                case LogType.Error:
                    return ConsoleColor.Magenta;
                case LogType.Critical:
                    return ConsoleColor.Red;
            }

            return Console.ForegroundColor;
        }


        private static void Write(LogType type, string format, params object[] args)
        {
            ConsoleColor typeColor,origColor;
            ulong mask = (ulong)type;

            string log = string.Format(format, args);

            logFile.Write(type.ToString() + ": " + log);

            if ((logMask & mask) == mask)
            {
                typeColor = GetColorByLogType(type);

                lock (consLock)
                {
                    origColor = Console.ForegroundColor;
                    Console.ForegroundColor = typeColor;

                    Console.Write(string.Format("{0}: ", type.ToString()));

                    Console.ForegroundColor = origColor;

                    Console.WriteLine(log);
                }
            }
        }

        public static string GetLogFileName(string type)
        {
            return string.Format("{0}-{1}.log", type, DateTime.Now.ToString("MM-dd-yyyy HH-mm-ss"));
        }

        public static void EnableAll()
        {
            EnableLogType(LogType.Critical | LogType.Error | LogType.Info | LogType.Verbose | LogType.Warning);
        }

        public static void DisableAll()
        {
            DisableLogType(LogType.Critical | LogType.Error | LogType.Info | LogType.Verbose | LogType.Warning);
        }

        public static void EnableLogType(LogType type)
        {
            ulong mask = (ulong)type;
            logMask |= mask;
        }

        public static void DisableLogType(LogType type)
        {
            ulong mask = (ulong)type;

            if ((logMask & mask) == mask)
            {
                logMask &= ~mask;
            }
        }

        public static void Verbose(string format, params object[] args)
        {
            Write(LogType.Verbose, format, args);
        }

        public static void Info(string format, params object[] args)
        {
            Write(LogType.Info, format, args);
        }

        public static void Warning(string format, params object[] args)
        {
            Write(LogType.Warning, format, args);
        }

        public static void Error(string format, params object[] args)
        {
            Write(LogType.Error, format, args);
        }

        public static void Critical(string format, params object[] args)
        {
            Write(LogType.Critical, format, args);
        }
    }
}

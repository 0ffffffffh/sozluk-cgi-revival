using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Logging
{
    class LogFileWriter
    {
        FileStream logFile;
        object lck = new object();
        string compType;
        readonly byte[] lineFeed = Encoding.ASCII.GetBytes("\r\n");
        Timer flushTimer;
        bool needsFlush=false;
        

        public LogFileWriter(string componentType)
        {
            compType = componentType;

            logFile = new FileStream(
                Log.GetLogFileName(compType), 
                FileMode.Append, 
                FileAccess.Write, 
                FileShare.Read
                );

            flushTimer = new Timer(new TimerCallback(FileStreamFlusher), null, 30 * 1000, 10 * 1000);
            
        }

        private void FileStreamFlusher(object o)
        {
            lock (lck)
            {
                if (needsFlush)
                {
                    logFile.Flush(true);
                    needsFlush = false;
                }
            }
        }

        public void Write(byte[] buffer, int writeLen)
        {
            lock (lck)
            {
                if (logFile == null)
                    return;

                if (logFile.Position > 2 * 1024 * 1024)
                {
                    logFile.Flush();
                    logFile.Dispose();

                    logFile = new FileStream(
                        Log.GetLogFileName(compType), 
                        FileMode.Append, 
                        FileAccess.Write, 
                        FileShare.Read
                        );
                }

                logFile.Write(buffer, 0, writeLen);
                logFile.Write(lineFeed, 0, lineFeed.Length);

                if (!needsFlush)
                    needsFlush = true;
                
            }
        }

        public void Write(string log)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(log);
            Write(buffer, buffer.Length);
        }

        public void Dispose()
        {
            lock (lck)
            {
                ManualResetEvent mre = new ManualResetEvent(false);
                flushTimer.Dispose(mre);

                mre.WaitOne();

                logFile.Dispose();
                logFile = null;
                mre.Dispose();
            }
        }
    }
}

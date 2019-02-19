using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace sozluk_backend.Core.Sys.Logging
{
    static class LogService
    {
        static Socket sock = null;
        static EndPoint localEp;
        static Thread worker;
        static bool running = true;
        static LogFileWriter logFile;

        private static void Recv()
        {
            byte[] buffer;
            byte[] lineFeed = Encoding.ASCII.GetBytes("\r\n");

            int readLen;

            buffer = new byte[1024];

            while (running)
            {
                try
                {
                    readLen = sock.ReceiveFrom(buffer, ref localEp);
                }
                catch
                {
                    readLen = 0;
                }

                if (readLen > 0)
                {
                    logFile.Write(buffer, readLen);
                }
            }
        }

        private static string GetLogFileName()
        {
            return Log.GetLogFileName("sozlukcgi-bridge");
        }

        public static void Start()
        {
            IPAddress localIp = IPAddress.Parse("127.0.0.1");

            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            localEp = new IPEndPoint(localIp, 2019);

            sock.Bind(localEp);

            logFile = new LogFileWriter("sozlukcgi-bridge");

            worker = new Thread(new ThreadStart(Recv));
            worker.Start();

        }

        public static void Stop()
        {
            running = false;
            sock.Close();
            sock.Dispose();

            logFile.Dispose();

            worker.Join();
        }


    }
}

using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys
{
    delegate void TalkCallback(Dictionary<string, string> v);

    class InternalTalk
    {
        Socket sock;
        IPAddress localIp;
        EndPoint ep;

        public InternalTalk(bool listener)
        {
            localIp = IPAddress.Parse("127.0.0.1");
            ep = new IPEndPoint(localIp, 9119);
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            if (listener)
            {
                sock.Bind(ep);
                RegisterReceiver();
            }
        }

        public Dictionary<string, string> Parse(byte[] data, int length)
        {
            Dictionary<string, string> talkInfo = new Dictionary<string, string>();

            string s = Encoding.ASCII.GetString(data,0,length);

            var items = s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Log.Verbose(s);
            foreach (var item in items)
            {
                var kv = item.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                if (!talkInfo.ContainsKey(kv[0].ToLower()))
                    talkInfo.Add(kv[0].ToLower(), kv[1]);

            }

            return talkInfo;
        }

        private void ReceiveData(IAsyncResult res)
        {
            byte[] buffer;
            int readLen;

            try
            {
                readLen = sock.EndReceiveFrom(res, ref ep);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                return;
            }

            buffer = (byte[])res.AsyncState;

            var talkData = Parse(buffer,readLen);

            if (OnTalk != null)
                OnTalk(talkData);

            talkData.Clear();
            talkData = null;
            buffer = null;

            RegisterReceiver();
        }

        private void RegisterReceiver()
        {
            byte[] data = new byte[512];

            try
            {
                sock.BeginReceiveFrom(
                    data,
                    0,
                    data.Length,
                    SocketFlags.None,
                    ref ep, new AsyncCallback(ReceiveData), data);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }
        }

        private bool SendPacket(string value)
        {
            byte[] data = Encoding.ASCII.GetBytes(value);

            try
            {
                return sock.SendTo(data, ep) > 0;
            }
            catch { }

            return false;
        }

        public void Stop()
        {
            sock.Dispose();
        }

        public void SendTalkData(string key, string value)
        {
            SendPacket(string.Format("{0}={1};", key, value));
        }

        public Dictionary<string, string> CreateTalkData()
        {
            return new Dictionary<string, string>();
        }

        public void SendTalkData(Dictionary<string, string> data)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var key in data.Keys)
            {
                sb.AppendFormat("{0}={1};", key, data[key]);
            }

            SendPacket(sb.ToString());

            sb.Clear();
            sb = null;
        }

        public void DisposeTalkData(ref Dictionary<string, string> data)
        {
            data.Clear();
            data = null;
        }

        public event TalkCallback OnTalk;

    }
}

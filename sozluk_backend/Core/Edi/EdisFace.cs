using sozluk_backend.Core.Cache;
using sozluk_backend.Core.Sys;
using sozluk_backend.Core.Sys.DataStore;
using sozluk_backend.Core.Sys.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace sozluk_backend.Core.Edi
{
    class EdisFace
    {
        AsyncCallback handlerCallback;
        int port;
        HttpListener httpListener;
        string cachedIndexHtml;
        object lck = new object();

        private Dictionary<string,string> BuildForm(HttpListenerContext ctx)
        {
            string[] items;

            string post;

            using (StreamReader sr = new StreamReader(ctx.Request.InputStream))
            {
                post = sr.ReadToEnd();
            }

            if (string.IsNullOrEmpty(post))
                return null;

            items = post.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

            var dict = new Dictionary<string, string>();

            foreach (var item in items)
            {
                string[] kv = item.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                if (kv.Length == 2)
                {
                    kv[1] = System.Web.HttpUtility.UrlDecode(kv[1],Encoding.GetEncoding("iso-8859-9"));


                    dict.Add(kv[0], kv[1]);
                }

            }

            return dict;
        }

        private string MimeTypeFromExt(string ext)
        {
            switch (ext)
            {
                case ".gif":
                    return "image/gif";
                case ".html":
                    return "text/html";
            }

            return "text/plain";
        }

        private bool ReplyWithFile(HttpListenerContext ctx, string fileName)
        {
            Stream fs;

            if (!File.Exists(fileName))
                return false;

            try
            {
                fs = File.OpenRead(fileName);
            }
            catch(Exception e)
            {
                Log.Error("{0} - {1}", fileName, e.Message);
                return false;
            }

            ctx.Response.ContentLength64 = fs.Length;
            ctx.Response.StatusCode = 200;

            ctx.Response.ContentEncoding = Encoding.ASCII;
            ctx.Response.ContentType = MimeTypeFromExt(Path.GetExtension(fileName));

            fs.CopyTo(ctx.Response.OutputStream);

            fs.Close();
            fs.Dispose();

            return true;
        }

        private bool ReplyIndex(HttpListenerContext ctx, string statusMessage)
        {
            string content = cachedIndexHtml.Replace("%%STATUS_MESSAGE%%", statusMessage);

            byte[] data = Encoding.ASCII.GetBytes(content);

            try
            {
                ctx.Response.ContentLength64 = data.Length;
                ctx.Response.StatusCode = 200;

                ctx.Response.ContentEncoding = Encoding.ASCII;
                ctx.Response.ContentType = "text/html";
                ctx.Response.OutputStream.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                data = null;
                return false;
            }

            data = null;
            return true;
        }

        private void CacheDeniedClient(string clientIp)
        {
            string key = Edi.MakeUniqueCacheKey("EDI_DENYLIST");

            HashSet<string> denyHashlist = null;

            lock (lck)
            {
                if (!CacheManager.TryGetCachedResult<HashSet<string>>(
                    key,
                    out denyHashlist)
                    )
                {
                    denyHashlist = new HashSet<string>();
                }


                if (!denyHashlist.Contains(clientIp))
                {
                    denyHashlist.Add(clientIp);
                    CacheManager.CacheObject(key, denyHashlist,TimeSpan.FromHours(3));
                }
            }

            denyHashlist.Clear();
            denyHashlist = null;
        }

        private bool IsClientDeniedAtThisSessionBefore(string clientIp)
        {
            bool denied = false;
            string key = Edi.MakeUniqueCacheKey("EDI_DENYLIST");

            HashSet<string> denyHashlist = null;

            lock (lck)
            {
                if (!CacheManager.TryGetCachedResult<HashSet<string>>(
                    key,
                    out denyHashlist)
                    )
                {
                    return false;
                }
            }

            denied = denyHashlist.Contains(clientIp);

            denyHashlist.Clear();
            denyHashlist = null;

            return denied;
        }

        private bool IsClientPermittedForSuserAddition(HttpListenerContext ctx, bool modify)
        {
            bool allowed;
            string clientIp;
            string query;
            

            clientIp = ctx.Request.RemoteEndPoint.Address.MapToIPv4().ToString();

            if (IsClientDeniedAtThisSessionBefore(clientIp))
            {
                return false;
            }

            SqlServerIo sql = SqlServerIo.Create();

            if (!sql.Ready)
                return false;

            query = "DECLARE @Permit BIT " +
                "EXEC IsRegistrationAllowed {0}, '{1}', @Permit OUTPUT " +
                "SELECT @Permit AS IsAllowed";
            
            
            sql.Execute(false, query, modify?1:0,clientIp);

            sql.Read();

            allowed = sql.GetValueOfColumn<bool>("IsAllowed");
            
            SqlServerIo.Release(sql);

            if (!allowed)
                CacheDeniedClient(clientIp);

            return allowed;
        }

        private void HandleAddSuser(HttpListenerContext ctx, string suserName, string pwd)
        {
            string replyText;


            if (!IsClientPermittedForSuserAddition(ctx,false))
            {
                Log.Warning("Too much user registration attempt on the same connection.");

                ReplyIndex(ctx, 
                    "oooooh ma$allah. aile epey geni$ sizde. dede, amca, torun torba iyidir in$allah." +
                    "ancak sorun $u ki, ayni baglanti uzerinden gunde en fazla 3 kayit yapabiliyorsunuz." +
                    "eksi sozluk'u bu kadar sevdiginizi bilmiyordum. gozlerim ya$ardi." +
                    "eh napalim, ailenin kalan bireylerini yarin kaydetmeyi dene.");
                return;
            }

            EdisHand.AddSuserResult result = EdisHand.AddSuser(suserName, pwd);

            Log.Info("Suser registration result for {0}, {1}", suserName, result.ToString());


            if (result == EdisHand.AddSuserResult.Joined)
            {
                IsClientPermittedForSuserAddition(ctx, true);

                ReplyIndex(ctx, 
                    suserName + 
                    " cok super bir nick secimi. muthi$ hatta o kadar begendim ki dayanamadim kaydediverdim. tebrik ediyorum seni.");

                return;
            }

            replyText = "olmadi. olamadi... cunku ";


            switch (result)
            {
                case EdisHand.AddSuserResult.HasNotAllowedChar:
                    replyText += "alt cizgi, tire, turkce karakter falan filan bunlar cok cirkin $eyler. ";
                    break;
                case EdisHand.AddSuserResult.TakenBefore:
                    replyText += "cok $anssizsin, senden once kapilmi$ bu nick. bugun evden di$ari cikma bence";
                    break;
                case EdisHand.AddSuserResult.TooLongOrShort:
                    replyText += "nick uzunlugunu gozden gecir bence";
                    break;
                case EdisHand.AddSuserResult.BadLuck:
                    replyText += "kotu $ans, tekrar deneyebilirsin. yuz yilda bir olacak $ey geldi seni buldu iyi mi?";
                    break;
            }

            ReplyIndex(ctx, replyText);

        }

        private void HandlePost(HttpListenerContext ctx, Dictionary<string,string> postForm)
        {
            if (postForm == null)
            {
                return;
            }

            if (postForm.ContainsKey("nick") && postForm.ContainsKey("pwd"))
            {
                HandleAddSuser(ctx, postForm["nick"], postForm["pwd"]);
            }
            else
            {
                ReplyIndex(ctx, "gerekli bir $eyler unuttun.");
            }
        }
        
        private void RequestHandler(IAsyncResult result)
        {
            Dictionary<string, string> form;

            string objectName,path;
            HttpListenerContext ctx;

            if (!this.httpListener.IsListening)
                return;

            try
            {
                ctx = this.httpListener.EndGetContext(result);
            }
            catch (Exception e)
            {
                Log.Error("request completion error: " + e.Message);
                RegisterRequestWaiter(null);
                return;
            }

            form = BuildForm(ctx);

            objectName = ctx.Request.Url.LocalPath;

            var ext = Path.GetExtension(objectName);
            objectName = Path.GetFileNameWithoutExtension(objectName);

            objectName = objectName.
                Replace(".", "").
                Replace("/","").
                Replace("\\","");

            if (string.IsNullOrEmpty(objectName))
                objectName = "\\";
            else
                objectName = "\\" + objectName + ext;

            path = Config.Get().HtmlContentRoot + objectName;
            
            RegisterRequestWaiter(null);

            if (objectName == "\\")
            {
                if (ctx.Request.HttpMethod.ToLower() == "post")
                {
                    HandlePost(ctx, form);
                }
                else
                    ReplyIndex(ctx, string.Empty);
            }
            else
            {
                ReplyWithFile(ctx, path);
            }
            
            ctx.Response.Close();
        }

        private void RegisterRequestWaiter(object state)
        {
            try
            {
                this.httpListener.BeginGetContext(this.handlerCallback, state);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }
        }

        public bool WipeClientInfos()
        {
            CacheManager.Remove(Edi.MakeUniqueCacheKey("EDI_DENYLIST"));

            SqlServerIo sql = SqlServerIo.Create();

            if (!sql.Ready)
                return false;

            if (!sql.Execute(true, "DELETE FROM ClientState;"))
            {
                SqlServerIo.Release(sql);
                return false;
            }

            SqlServerIo.Release(sql);
            return true;
        }

        public EdisFace(int port)
        {
            this.port = port;
            this.handlerCallback = new AsyncCallback(RequestHandler);

#if DEBUG
            WipeClientInfos();
#endif
            this.httpListener = new HttpListener();
            this.httpListener.Prefixes.Add(string.Format("http://localhost:{0}/", port));
            this.httpListener.Prefixes.Add(string.Format("http://sourtimes.oguzkartal.net:{0}/", port));

            this.httpListener.Start();

            cachedIndexHtml = File.ReadAllText(Config.Get().HtmlContentRoot + "\\edi.html");

            if (this.httpListener.IsListening)
                RegisterRequestWaiter(null);
            else
                Log.Warning("edi service could not be started");

        }

        public void Close()
        {
            this.httpListener.Stop();
            this.httpListener.Close();
        }

        public bool IsAlive
        {
            get { return this.httpListener.IsListening; }
        }
    }
}

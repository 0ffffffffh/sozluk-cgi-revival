using sozluk_backend.Core.Sys.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Model
{
    [Serializable]
    class Entry
    {
        public static Entry FromResponse(RequestObject req)
        {
            if (!req.HasItem("Baslik"))
                return null;

            if (!req.HasItem("Suser"))
                return null;

            if (!req.HasItem("Date"))
                return null;

            if (!req.HasItem("Desc"))
                return null;

            return new Entry(
                req.GetItem<string>("Baslik"),
                req.GetItem<string>("Suser"),
                req.GetItem<string>("Date"),
                req.GetItem<string>("Desc"));
        }

        private string MakeTag(string tagName, object value, string attribName = null, object attribValue = null)
        {
            if (!string.IsNullOrEmpty(attribName))
                return string.Format("<{0} {1}=\"{2}\">{3}</{0}>", tagName, attribName, attribValue.ToString(), value);

            return string.Format("<{0}>{1}</{0}>", tagName, value);
        }

        public string GetTransportString()
        {
            StringBuilder sb = new StringBuilder();

            string s;

            if (RepCount > 1)
                sb.Append(MakeTag("Baslik", this.Baslik, "RepCount", RepCount)); 
            else
                sb.Append(MakeTag("Baslik", this.Baslik));

            sb.Append(MakeTag("Suser", Suser));
            sb.Append(MakeTag("Date", Date));
            sb.Append(MakeTag("Desc", Content));

            s = sb.ToString();
            sb.Clear();
            sb = null;
            
            return s;
        }


        private string PrepareBaslik(string s)
        {
            return s.Replace("<", "").Replace(">", "");
        }

        public Entry(string baslik, string suser, string date, string desc)
        {
            DateTime dt;

            Baslik = baslik;
            Suser = suser.Trim();

            if (!string.IsNullOrEmpty(date))
            {
                if (!DateTime.TryParse(date, out dt))
                    Date = DateTime.Now;
                else
                    Date = dt;
            }

            Content = desc;
        }

        public Entry(string baslik, string suser, string date, string desc, int repCount)
            : this(baslik,suser,date,desc)
        {
            RepCount = repCount;
        }


        public int BaslikID
        {
            get;
            private set;
        }

        public string Baslik
        {
            get;
            private set;
        }

        public string Suser
        {
            get;
            private set;
        }

        public DateTime Date
        {
            get;
            private set;
        }

        public string Content
        {
            get;
            private set;
        }

        public int RepCount
        {
            get;
            set;
        }

        public void SetId(int id)
        {
            if (this.BaslikID==0)
            {
                this.BaslikID = id;
            }
        }

        public void SecureFields()
        {
            Baslik = PrepareBaslik(InputHelper.SanitizeForSQL(Baslik));
            Suser = InputHelper.SanitizeForSQL(Suser);
            Content = InputHelper.SanitizeForXSS(Content);
        }

        public void FixForMultipleLineFeeds()
        {
            int index=0;

            while (index != -1)
            {
                index = Content.IndexOf("\r\n", index);

                if (index != -1)
                {
                    Content = Content.Remove(index, 2);

                    while (index + 2 < Content.Length)
                    {
                        if (Content.Substring(index, 2) == "\r\n")
                        {
                            Content = Content.Remove(index, 2).Insert(index, "<br/>");
                            index += 5;
                        }
                        else
                        {
                            Content = Content.Insert(index, "\r\n");
                            index += 2;
                            break;
                        }

                    }

                    if (index + 2 > Content.Length)
                        break;

                }
            }
        }
    }
}

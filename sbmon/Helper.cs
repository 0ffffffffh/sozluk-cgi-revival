using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sbmon
{
    class Helper
    {
        public static Dictionary<string, string> ParseOptions(string s)
        {
            Dictionary<string, string> argDict = new Dictionary<string, string>();

            string tmp = "";
            bool putTmp = false;
            string opt = null;

            if (string.IsNullOrEmpty(s))
                return argDict;

            string[] breaks = s.Split(' ');

            foreach (string item in breaks)
            {
                if (item.StartsWith("\""))
                {
                    tmp = item;
                    putTmp = true;
                }
                else if (item.EndsWith("\""))
                {
                    putTmp = false;

                    if (opt != null)
                    {
                        argDict.Add(opt, tmp + item);
                        opt = null;
                    }
                    else
                        argDict.Add(tmp + item, "");

                    tmp = "";
                }
                else
                {
                    if (putTmp)
                        tmp += item;
                    else
                    {
                        var value = item.Trim();

                        if (value.Length > 0)
                        {
                            if (value.StartsWith("-"))
                            {
                                if (opt != null)
                                {
                                    argDict.Add(opt, "");
                                }

                                opt = value;

                            }
                            else
                            {
                                if (opt != null)
                                {
                                    argDict.Add(opt, value);
                                    opt = null;
                                }
                                else
                                    argDict.Add(value, "");
                            }
                        }
                    }
                }

            }

            if (opt != null)
                argDict.Add(opt, "");

            breaks = null;

            return argDict;
        }
    }
}

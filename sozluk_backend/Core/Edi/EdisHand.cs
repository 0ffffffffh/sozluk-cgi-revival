using sozluk_backend.Core.Sys.DataStore;
using sozluk_backend.Core.Sys.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Edi
{
    class EdisHand
    {
        internal enum AddSuserResult
        {
            Joined,
            TakenBefore,
            HasNotAllowedChar,
            TooLongOrShort,
            BadLuck
        }


        public static AddSuserResult AddSuser(string suser, string pwd)
        {
            Suser suserObject;
            bool registered;

            string pwdHash = Suser.SecurePassword(pwd);

            suser = suser.Trim();

            if (!Suser.IsSuserNameAllowed(suser))
                return AddSuserResult.HasNotAllowedChar;

            if (suser.Length < 3 || suser.Length > 32)
                return AddSuserResult.TooLongOrShort;

            suserObject = new Suser(0, suser, pwdHash);

            if (SozlukDataStore.AddSuser(suserObject, out registered))
            {
                if (!registered)
                    return AddSuserResult.TakenBefore;
            }
            else
                return AddSuserResult.BadLuck;


            return AddSuserResult.Joined;
        }
    }
}

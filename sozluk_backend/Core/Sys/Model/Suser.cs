using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Model
{
    [Serializable]
    class Suser
    {
        private const string SALT1 = "3+p07zx37s%1";
        private const string SALT2 = "fx*?+32#a";

        public static string SecurePassword(string pass)
        {
            return Helper.Sha512(SALT1 + FixPassword(pass) + SALT2);
        }

        private static bool IsAnsiLetterOrDigit(char c)
        {
            if (c >= '0' && c <= '9')
                return true;

            if (c >= 'a' && c <= 'z')
                return true;

            if (c >= 'A' && c <= 'Z')
                return true;
            
            return false;
        }

        private static bool IsAllowedChr(char c)
        {
            if (IsAnsiLetterOrDigit(c))
                return true;

            if (c == ' ')
                return true;

            return false;
        }

        public static bool IsSuserNameAllowed(string suser)
        {
            if (string.IsNullOrEmpty(suser))
                return false;
            
            foreach (var chr in suser)
            {
                if (!IsAllowedChr(chr))
                    return false;
            }

            return true;
        }

        public static string FixPassword(string pwd)
        {
            return pwd.ToLower();
        }

        public Suser(ulong id, string suser, string passHash)
        {
            this.InternalId = id;
            this.SuserName = suser.Trim().ToLower();
            this.PasswordHash = passHash;
        }

        public ulong InternalId
        {
            get;
            private set;
        }

        public string SuserName
        {
            get;
            private set;
        }

        public string PasswordHash
        {
            get;
            private set;
        }

        public bool ValidatePassword(string possiblePassword)
        {
            return SecurePassword(possiblePassword) == this.PasswordHash;
        }
    }
}

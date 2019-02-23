using sozluk_backend.Core.Sys.DataStore;
using sozluk_backend.Core.Sys.Model;
using sozluk_backend.Core.Sys.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.Handlers
{
    class AuthenticateSuserHandler : SozlukRequestHandlerBase
    {
        public AuthenticateSuserHandler(RequestObject req) : 
            base(req)
        {

        }

        public override bool Process()
        {
            Suser suserObj = null;

            string suser, pwd;

            suser = GetValue<string>("Suser");


            pwd = GetValue<string>("Pass",false);


            if (!Suser.IsSuserNameAllowed(suser))
            {
                PushResponseItem("AuthStatus", "AuthFailed");
                return true;
            }

            if (suser != null && pwd != null)
                suserObj = SozlukDataStore.GetSuser(suser);

            if (suserObj != null && suserObj.ValidatePassword(pwd))
            {
                PushResponseItem("AuthStatus", "AuthSuccess");
            }
            else
            {
                PushResponseItem("AuthStatus", "AuthFailed");
            }

            return true;
        }
    }
}

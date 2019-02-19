using sozluk_backend.Core.Sys.Request;

namespace sozluk_backend.Core.Sys.Handlers
{
    interface ISozlukRequestHandler
    {
        bool Process();
        bool PushBackToBridge();
        void CompleteRequest();
    }
}

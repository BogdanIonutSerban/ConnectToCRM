using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class WebResult
    {
        public HttpResponseMessage Response { get; set; } = null;
        public Exception Exception { get; set; } = null;
        public bool EncounterError { get; set; } = false;

        public string ReturnedContent { get; set; } = null;
    }
}

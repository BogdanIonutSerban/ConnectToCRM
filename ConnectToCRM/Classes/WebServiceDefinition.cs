using ConnectToCRM.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class WebServiceDefinition
    {
        // public WebServiceType WebServiceType { get; set; }

        public string EndpointAddress { get; set; }
        public Verb MessageVerb { get; set; }

        public bool UseAuth { get; set; }

        public Dictionary<string, string> Headers;

        public int Timeout { get; set; }

    }
    public class WebServiceAuthDefinition
    {
        // public WebServiceType WebServiceType { get; set; }

        public string AuthEndpoint { get; set; }

        public Dictionary<string, string> AuthHeader { get; set; }
    }
}

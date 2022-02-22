using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectToCRM.Enums
{
    public enum AutomationStatus
    {
        Ok = 1,
        NotOk = 0
    }
    public enum Verb
    {
        GET = 0,
        POST = 1,
        PUT = 2,
        PATCH = 3,
        DELETE = 4
    }    
}

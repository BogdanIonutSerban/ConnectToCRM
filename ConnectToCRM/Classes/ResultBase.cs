using ConnectToCRM.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class ResultBase
    {

        public string ResultObject { get; }

        public AutomationStatus Status { get; }

        public string Error { get; }

        public string ResponseDetails { get; }

        public ResultBase(WebResult result, string otherMsg = null)
        {
            if (result == null) throw new Exception("WebResult param null!");

            ResponseDetails += result.Response != null ? $"Is Success Status Code: {result.Response.IsSuccessStatusCode}{Environment.NewLine}" : string.Empty;
            ResponseDetails += result.Response != null ? $"Status Code: {result.Response.StatusCode}{Environment.NewLine}" : string.Empty;
            if (otherMsg != null) ResponseDetails += otherMsg;
            if (!result.EncounterError)
            {
                Status = AutomationStatus.Ok;
                ResultObject = result.ReturnedContent;
                Error = null;
            }
            else
            {
                Status = AutomationStatus.NotOk;
                ResultObject = result.ReturnedContent;
                var inner = result.Exception != null && result.Exception.InnerException != null ? result.Exception.InnerException.Message : string.Empty;
                Error = result.Exception != null ? ($"Exception: {result.Exception.Message}{Environment.NewLine}InnerException: {inner} ") : null;
            }
        }
    }
}

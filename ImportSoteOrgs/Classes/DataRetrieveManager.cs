using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConnectToCRM.Classes
{
    public class DataRetrieveManager
    {
        public RequestObject RequestData { get; private set; }

        public DataRetrieveManager(RequestObject _requestData)
        {
            RequestData = _requestData;
        }

        public ConceptCodes GetOrganizations(int pageNo)
        {
            string result = GetOrganisationsFromCodeserver(pageNo);
            return JsonConvert.DeserializeObject<ConceptCodes>(result);
        }
        string GetOrganisationsFromCodeserver(int pageNo)
        {
            string response;

            WebServiceDefinition webServiceDefinition = GetWebserviceDef();
            WebSeviceCall webSeviceCall = GetWebserviceCall( webServiceDefinition, pageNo);

            try
            {
                var result = webSeviceCall.GetResult();
                response = result.ResultObject;
            }
            catch (Exception ex)
            {
                return $"Error on calling Web Service !{Environment.NewLine}Error: {ex.Message}";
            }
            return response;
        }

        WebServiceDefinition GetWebserviceDef()
        {
            var id = RequestData.ClassificationId;
            return new WebServiceDefinition()
            {
                EndpointAddress = $"https://koodistopalvelu.kanta.fi/codeserver/csapi/v3/classifications/{id}/conceptcodes?<queryParams>",
                MessageVerb = Enums.Verb.GET,
                UseAuth = false,
                Timeout = 60,
                Headers = new Dictionary<string, string>()
            };
        }
        WebSeviceCall GetWebserviceCall(WebServiceDefinition webServiceDefinition, int pageNo)
        {
            string modifiedAfterParam = string.Empty;
            if (RequestData.ExecutionType == ExeucutionType.DailyUpsert)
            {
                if (RequestData.ModifiedAfter != null)
                {
                    var dateVal = (DateTimeOffset)RequestData.ModifiedAfter;
                    modifiedAfterParam = $"&modifiedAfter={dateVal:yyyy-MM-dd}";
                }
                else{
                    var dateVal = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                    modifiedAfterParam = $"&modifiedAfter=" + dateVal;
                }
            }
            
            WebSeviceCall webSeviceCall;
            try
            {
                string queryParams = $"status=ACTIVE{modifiedAfterParam}&sortBy=CONCEPTCODEID&pageSize=500&page={pageNo}";
                webSeviceCall = new WebSeviceCall(webServiceDefinition, queryParams);


                webSeviceCall.PayloadJson = string.Empty;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return webSeviceCall;
        }
    }
}

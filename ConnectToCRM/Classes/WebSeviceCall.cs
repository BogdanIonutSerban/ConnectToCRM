using ConnectToCRM.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ConnectToCRM.Classes
{
    public class WebSeviceCall : IWebServiceResult
    {
        private WebServiceDefinition WsDef;
        public string PayloadJson { get; set; }
        public string endpointAddress { get; set; }
        private bool debug = false;
        private string OtherMsg = string.Empty;

        public WebSeviceCall(WebServiceDefinition webServiceDefinition, string queryParams = "")
        {
            OtherMsg += Environment.NewLine + " ggg " + queryParams + Environment.NewLine;
            WsDef = webServiceDefinition;
            debug = true;
            if (!string.IsNullOrWhiteSpace(queryParams))
            {
                WsDef.EndpointAddress = WsDef.EndpointAddress.Replace("<queryParams>", queryParams);
            }
            var maxURLlen = 2050;
            endpointAddress = WsDef.EndpointAddress.Length > maxURLlen ? WsDef.EndpointAddress.Substring(0, maxURLlen) : WsDef.EndpointAddress;
        }
        public ResultBase GetResult()
        {
            var task = MakeCall();
            task.Wait();

            if (debug)
            {
                OtherMsg += GetHeaderContent();
            }
            if (!debug)
                return new ResultBase(task.Result);
            else
                return new ResultBase(task.Result, OtherMsg);
        }
        public async Task<WebResult> MakeCall()
        {
            try
            {
                HttpClient client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(WsDef.Timeout)
                };

                if (WsDef.Headers.Any())
                {
                    foreach (var header in WsDef.Headers)
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = null;

                var content = new StringContent(
                      PayloadJson,
                     System.Text.Encoding.UTF8,
                     "application/json"
                     );

                switch (WsDef.MessageVerb)
                {
                    case (Verb.POST):
                        {
                            response = await client.PostAsync(new Uri(WsDef.EndpointAddress), content);
                        }
                        break;
                    case (Verb.PUT):
                        {
                            response = await client.PutAsync(new Uri(WsDef.EndpointAddress), content);
                        }
                        break;
                    case (Verb.PATCH):
                        {
                            var request = new HttpRequestMessage(new HttpMethod("PATCH"), new Uri(WsDef.EndpointAddress)) { Content = content };
                            response = await client.SendAsync(request);
                        }
                        break;
                    case (Verb.DELETE):
                        {
                            response = await client.DeleteAsync(new Uri(WsDef.EndpointAddress));
                        }
                        break;
                    case (Verb.GET):
                        {
                            response = await client.GetAsync(new Uri(WsDef.EndpointAddress));
                        }
                        break;
                    default:
                        {
                            return new WebResult
                            {
                                EncounterError = true,
                                Exception = new Exception($"MessageVerb {WsDef.MessageVerb} not implemented!"),
                                Response = null
                            };
                        }
                }


                if (response.IsSuccessStatusCode)
                {
                    string responseContent = null;
                    if (response != null && response.Content != null)
                        responseContent = await response.Content.ReadAsStringAsync();
                    return new WebResult
                    {
                        EncounterError = false,
                        Exception = null,
                        Response = response,
                        ReturnedContent = responseContent
                    };
                }
                else
                {

                    OtherMsg += $"{JsonConvert.SerializeObject(response)}{Environment.NewLine}";
                    string responseContent = null;
                    if (response != null && response.Content != null)
                        responseContent = await response.Content.ReadAsStringAsync();
                    return new WebResult
                    {
                        EncounterError = true,
                        Exception = null,
                        Response = response,
                        ReturnedContent = responseContent
                    };
                }

            }
            catch (HttpRequestException ex1)
            {
                OtherMsg += $"Exceptie Http call1 {Environment.NewLine}";
                return new WebResult
                {
                    EncounterError = true,
                    Exception = ex1,
                    Response = null
                };
            }
            catch (Exception ex2)
            {
                OtherMsg += $"Exceptie call {ex2} {Environment.NewLine}";
                return new WebResult
                {
                    EncounterError = true,
                    Exception = ex2,
                    Response = null
                };
            }
        }


        private string GetHeaderContent()
        {
            var headerDef = string.Empty;
            if (WsDef.Headers != null && WsDef.Headers.Any())
            {
                foreach (var h in WsDef.Headers)
                {
                    headerDef += $"{h.Key} = {h.Value}{Environment.NewLine}";
                }
            }
            return headerDef;
        }
    }
}

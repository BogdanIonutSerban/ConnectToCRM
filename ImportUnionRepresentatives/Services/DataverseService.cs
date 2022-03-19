using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Rest;

namespace ConnectToCRM.Services
{
    public class DataverseService
    {
        public string CrmUrl { get; set; }
        public string ClientId { get; set; }
        private string SecretValue { get; set; }

        public ServiceClient CreateServiceClient()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            ServiceClient service = new ServiceClient($"AuthType=ClientSecret;ClientId=905a40cd-3aa7-42e6-82cf-4ff6c9d53962;ClientSecret=ALg7Q~cJuwXkErHEo3hy5CIrQiSy54lvsCRGt;Url=https://aapelitest.crm4.dynamics.com");
            //
            if (!service.IsReady)
            {
                return null;
            }
            return service;
        }

    }
}

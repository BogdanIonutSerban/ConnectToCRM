using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ImportUnionRepresentatives.Services
{
    public class AzureBlobService
    {
        public string GetFileFromAzure(string containerName, string fileName)
        {
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName=elslaakariliittoprocount;AccountKey=79+g28gQr3hb/5tcwWDJ7F5s5ZKgClB66HrtGL8QuRfvwrpz9qWSDoamrrseChqtrNTX/kxB7yvfOEhcFayFzg==;EndpointSuffix=core.windows.net";

            // Setup the connection to the storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            // Connect to the blob storage
            CloudBlobClient serviceClient = storageAccount.CreateCloudBlobClient();
            // Connect to the blob container
            CloudBlobContainer container = serviceClient.GetContainerReference($"{containerName}");
            // Connect to the blob file
            CloudBlockBlob blob = container.GetBlockBlobReference($"{fileName}");

            // Get the blob file as text
            string contents = blob.DownloadTextAsync().Result;
/*            List<UnionRepresentativeContainer> results = new List<UnionRepresentativeContainer>();

            using (var stream = new MemoryStream())
            {
                blob.DownloadToStreamAsync(stream);
                stream.Position = 0;//resetting stream's position to 0
                var serializer = new JsonSerializer();

                using (var sr = new StreamReader(stream))
                {
                    using (var jsonTextReader = new JsonTextReader(sr))
                    {
                        var result = serializer.Deserialize(jsonTextReader);
                    }
                }
            }*/


            return contents;
        }
    }
}

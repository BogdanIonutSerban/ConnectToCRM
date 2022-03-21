using Azure.Storage.Blobs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;

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

            return contents;
        }

        public void MoveProcessedFileToArchive(string sourceContainerName, string destinationContainerName, string fileName)
        {
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName=elslaakariliittoprocount;AccountKey=79+g28gQr3hb/5tcwWDJ7F5s5ZKgClB66HrtGL8QuRfvwrpz9qWSDoamrrseChqtrNTX/kxB7yvfOEhcFayFzg==;EndpointSuffix=core.windows.net";

            // Setup the connection to the storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            // Connect to the blob storage
            CloudBlobClient serviceClient = storageAccount.CreateCloudBlobClient();
            // Connect to the blob container
            CloudBlobContainer sourceContainer = serviceClient.GetContainerReference($"{sourceContainerName}");
            // Connect to the blob file
            CloudBlockBlob blob = sourceContainer.GetBlockBlobReference($"{fileName}");

            // Get the blob file as text
            var contents = blob.DownloadTextAsync();

            CloudBlobContainer destinationContainer = serviceClient.GetContainerReference($"{destinationContainerName}");
            // Connect to the blob file
            string destFileName = "Representatives" + DateTime.Now.ToString("yyyymmdd");
            CloudBlockBlob destBlob = destinationContainer.GetBlockBlobReference($"{destFileName}");
            
            destBlob.StartCopyAsync(blob);
            blob.DeleteIfExistsAsync();
        }
    }
}

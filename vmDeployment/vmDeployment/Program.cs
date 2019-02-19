using System;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace vmDeployment
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create the management client

            var credentials = SdkContext.AzureCredentialsFactory
                .FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));

            var azure = Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithDefaultSubscription();

            // Create a resource group

            var groupName = "myResourceGroup";
            var location = Region.USCentral;

            var resourceGroup = azure.ResourceGroups.Define(groupName)
                .WithRegion(location)
                .Create();

            // Create a storage account

            string storageAccountName = SdkContext.RandomResourceName("st", 10);

            Console.WriteLine("Creating storage account...");
            var storage = azure.StorageAccounts.Define(storageAccountName)
                .WithRegion(Region.USCentral)
                .WithExistingResourceGroup(resourceGroup)
                .Create();

            var storageKeys = storage.GetKeys();
            string storageConnectionString = "DefaultEndpointsProtocol=https;"
                + "AccountName=" + storage.Name
                + ";AccountKey=" + storageKeys[0].Value
                + ";EndpointSuffix=core.windows.net";

            var account = CloudStorageAccount.Parse(storageConnectionString);
            var serviceClient = account.CreateCloudBlobClient();

            Console.WriteLine("Creating container...");
            var container = serviceClient.GetContainerReference("templates");
            container.CreateIfNotExistsAsync().Wait();
            var containerPermissions = new BlobContainerPermissions()
            { PublicAccess = BlobContainerPublicAccessType.Container };
            container.SetPermissionsAsync(containerPermissions).Wait();

            Console.WriteLine("Uploading template file...");
            var templateblob = container.GetBlockBlobReference("CreateVMTemplate.json");
            templateblob.UploadFromFileAsync("..\\..\\CreateVMTemplate.json");

            Console.WriteLine("Uploading parameters file...");
            var paramblob = container.GetBlockBlobReference("Parameters.json");
            paramblob.UploadFromFileAsync("..\\..\\Parameters.json");

            // Deploy a template

            Console.WriteLine("Deploying the uploaded VM template...");
            var templatePath = "https://" + storageAccountName + ".blob.core.windows.net/templates/CreateVMTemplate.json";
            var paramPath = "https://" + storageAccountName + ".blob.core.windows.net/templates/Parameters.json";
            var deployment = azure.Deployments.Define("myDeployment")
                .WithExistingResourceGroup(groupName)
                .WithTemplateLink(templatePath, "1.0.0.0")
                .WithParametersLink(paramPath, "1.0.0.0")
                .WithMode(Microsoft.Azure.Management.ResourceManager.Fluent.Models.DeploymentMode.Incremental)
                .Create();
            Console.WriteLine("Deployed!");
            Console.WriteLine("Press enter to delete the resource group...");
            Console.ReadLine();

            // Delete the resources 

            azure.ResourceGroups.DeleteByName(groupName);

        }
    }
}

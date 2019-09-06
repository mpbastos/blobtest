using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace BlobTest.Authentication
{
    public class TokenService
    {

        readonly ILogger _logger;
        readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration,
                ILoggerFactory logger)
        {
            _logger = logger?.CreateLogger<TokenService>() ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<Microsoft.Identity.Client.AuthenticationResult> GetServiceTokenbyCertificate(string resource)
        {

            // Even if this is a console application here, a daemon application is a confidential client application
            IConfidentialClientApplication app;

            X509Certificate2 certificate;

            // If there is a name on the certificate we use the machine store an look up for that certificate
            // The current user must have access to the certificate keys
            var certName = _configuration["AppId:certificate:name"];
            if(!String.IsNullOrEmpty(certName))
            {
                _logger.LogInformation($"Using machine store certifcate.");
                certificate = ReadCertificate(certName);
            }
            else // otherwhise we get the cert from a key vault
            {
                _logger.LogInformation($"Using Azure Key Vault");
                var secret = await GetSecretFromKV();
                certificate = new X509Certificate2(Convert.FromBase64String(secret));
            }

            string authority = _configuration["AppId:authority"];

            app = ConfidentialClientApplicationBuilder.Create(_configuration["AppId:id"])
                .WithCertificate(certificate)
                .WithAuthority(new Uri(authority))
                .Build();

            // With client credentials flows the scopes is ALWAYS of the shape "resource/.default", as the 
            // application permissions need to be set statically (in the portal or by PowerShell), and then granted by
            // a tenant administrator
            string[] scopes = new string[] { $"{resource}/.default" };

            Microsoft.Identity.Client.AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenForClient(scopes)
                    .ExecuteAsync();
                _logger.LogInformation("Token acquired");
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be of the form "https://resourceurl/.default"
                // Mitigation: change the scope to be as expected
                _logger.LogError("Scope provided is not supported");
            }

            return result;
        }

        private X509Certificate2 ReadCertificate(string certificateName)
        {
            _logger.LogInformation($"Certificate name:{certificateName}");
            if (string.IsNullOrWhiteSpace(certificateName))
            {
                throw new ArgumentException("certificateName should not be empty. Please set the CertificateName setting in the appsettings.json", "certificateName");
            }
            X509Certificate2 cert = null;

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = store.Certificates;

                // Find unexpired certificates.
                X509Certificate2Collection currentCerts = certCollection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);

                // From the collection of unexpired certificates, find the ones with the correct name.
                X509Certificate2Collection signingCert = currentCerts.Find(X509FindType.FindBySubjectDistinguishedName, certificateName, false);

                // Return the first certificate in the collection, has the right name and is current.
                cert = signingCert.OfType<X509Certificate2>().OrderByDescending(c => c.NotBefore).FirstOrDefault();
            }
            return cert;
        }
      
      private async Task<string> GetSecretFromKV()
        {

            _logger.LogInformation($"Key vault uri:{_configuration["AppId:certificate:keyVaultUri"]} Secrete Name:{_configuration["AppId:certificate:secret"]}");
            
            // The code uses AzureServiceTokenProvider to get access to the key vault.
            // Make sure to enable managed identity for the vm/service and grant access to get screts on the key vault.
            // https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview
            var azureServiceTokenProvider = new AzureServiceTokenProvider();

            var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

            var secret = await kvClient.GetSecretAsync(_configuration["AppId:certificate:keyVaultUri"], _configuration["AppId:certificate:secret"]);

            return secret.Value;

        }

    }
}

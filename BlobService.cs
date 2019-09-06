using BlobTest.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BlobTest
{
    class BlobService
    {
        HttpClient _httpClient = new HttpClient();

        readonly TokenService _tokenService;

        readonly ILogger _logger;
        readonly IConfiguration _configuration;

        public BlobService(IConfiguration configuration,
                ILoggerFactory logger,
                TokenService tokenService)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _logger = logger?.CreateLogger<BlobService>() ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }


        private async Task<string> SendRequestUsingToken(HttpMethod verb, string url, string blobType, HttpContent content)
        {
            var serviceVer = "2019-02-02";
            
            // Acuire token for Azure storage
            var authResult = await _tokenService.GetServiceTokenbyCertificate("https://storage.azure.com/");
            _logger.LogInformation("storage account token aquired.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);

            string txt = null ;
            HttpRequestMessage request = new HttpRequestMessage(verb, url);
            request.Headers.Add("x-ms-version", serviceVer);
            request.Headers.Add("x-ms-blob-type",blobType);

            request.Content = content;

            try
            {

                HttpResponseMessage response = await _httpClient.SendAsync(request); //.ConfigureAwait(false);
                _logger.LogInformation($"Status Conde:{response.StatusCode}");

                txt = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogInformation($"Request resoponse:{txt}");

            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error send request to {url}");
            }
            

            return txt;
        }

        public async Task<string> AppendBlobToken(string blobName, string content)
        {
            var url = $"https://wvdselfhost.blob.core.windows.net/testmpb/{blobName}?comp=appendblock";

            var path = $"testmpb/{blobName}";
            var strcontent = new StringContent(content);

            var ret = await SendRequestUsingToken(HttpMethod.Put, url ,"AppendBlob", strcontent);
            Console.WriteLine("End Append.");
            return ret;

        }
        public async Task<string> CreateAppendBlobToken(string blobName)
        {
            var url = $"https://wvdselfhost.blob.core.windows.net/testmpb/{blobName}";

            var path = $"testmpb/{blobName}";

            return await SendRequestUsingToken(HttpMethod.Put, url ,"AppendBlob", null);

        }
        public async Task<string> PutBlobTokenAsync(string blobName, string content)
        {
            var url = $"{_configuration["storageAccount:url"]}/{_configuration["storageAccount:container"]}/{blobName}";

            var strcontent = new StringContent(content);

            return await SendRequestUsingToken(HttpMethod.Put, url ,"BLockBlob", strcontent);

        }



    }
}

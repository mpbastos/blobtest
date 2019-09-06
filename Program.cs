using BlobTest.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BlobTest
{


    class Program
    {

        static async Task Main(string[] args)
        {

            var fi = new FileInfo(args[0]);

            string content = File.ReadAllText(args[0]);
            string blobName = fi.Name;

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);

            var Configuration = builder.Build();

            var serviceProvider = new ServiceCollection()
                .AddSingleton<TokenService>()
                .AddSingleton<BlobService>()
                .AddSingleton<IConfiguration>(Configuration)
                .AddLogging(loggingBuilder =>
                 {
                     loggingBuilder.AddConfiguration(Configuration.GetSection("Logging"));
                     loggingBuilder.AddConsole();
                 })
                .BuildServiceProvider();

            var _Logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger("App");

            var bs = serviceProvider.GetService<BlobService>();

            try
            {
                _Logger.LogInformation("Sending blob...");
                await bs.PutBlobTokenAsync(blobName, content);
                _Logger.LogInformation("Put blob operation completed.");
            }
            catch (Exception e)
            {
                _Logger.LogError(e, "Exception when calling PutBlobTokenAsync.");
            }

            serviceProvider.Dispose();

        }
    }
}

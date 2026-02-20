using Microsoft.Extensions.Logging;
using Dnp.S3.Browser.Core.Interfaces;
using Dnp.S3.Browser.Services.Local;
using Dnp.S3.Browser.ViewModels.ViewModels;
using Dnp.S3.Browser.UI.Pages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Maui.Storage;
using System.IO;
using Microsoft.Extensions.Configuration;
using Amazon;
using Amazon.Runtime;

namespace Dnp.S3.Browser.UI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // Add configuration from appsettings.json (optional)
            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            // Caching for responsiveness
            builder.Services.AddMemoryCache();

            // Gate IS3Service registration based on configuration setting 'UseLocalS3'.
            // Default = false => use AWS. Set UseLocalS3 = true in appsettings.json to use LocalS3Service.
            var useLocal = builder.Configuration.GetValue<bool?>("UseLocalS3") ?? false;

            if (useLocal)
            {
                builder.Services.AddSingleton<IS3Service>(sp =>
                {
                    var cache = sp.GetRequiredService<IMemoryCache>();
                    var root = Path.Combine(FileSystem.AppDataDirectory, "LocalS3");
                    return new LocalS3Service(root, cache);
                });
            }
            else
            {
                // Register AWS S3 client and service
                string? region = builder.Configuration["AWS:Region"];
                Amazon.RegionEndpoint? regionEndpoint = null;
                if (!string.IsNullOrEmpty(region))
                {
                    regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
                }

                // Allow explicit credentials for testing via appsettings.json: AWS:AccessKey and AWS:SecretKey.
                var accessKey = builder.Configuration["AWS:AccessKey"];
                var secretKey = builder.Configuration["AWS:SecretKey"];

                builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
                {
                    if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                    {
                        var creds = new BasicAWSCredentials(accessKey, secretKey);
                        if (regionEndpoint != null)
                            return new Amazon.S3.AmazonS3Client(creds, regionEndpoint);
                        return new Amazon.S3.AmazonS3Client(creds);
                    }

                    if (regionEndpoint != null)
                        return new Amazon.S3.AmazonS3Client(regionEndpoint);
                    return new Amazon.S3.AmazonS3Client();
                });

                builder.Services.AddSingleton<IS3Service, Dnp.S3.Browser.Services.Aws.AwsS3Service>();
            }

            // ViewModel and pages
            builder.Services.AddTransient<S3BrowserViewModel>();
            builder.Services.AddTransient<S3BrowserPage>();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

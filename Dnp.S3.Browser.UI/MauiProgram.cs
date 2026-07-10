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

            // Theme resources are set in App once the Application instance exists.

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

                // MFA prompter (UI) - used when explicit credentials are provided and MFA ARN present
                builder.Services.AddSingleton<Dnp.S3.Browser.UI.Services.IMfaPrompter, Dnp.S3.Browser.UI.Services.MfaPrompter>();
                // Session store to cache created session credentials for app lifetime
                builder.Services.AddSingleton<Dnp.S3.Browser.UI.Services.AwsSessionStore>();

                // Register AwsS3Service with a lazy client factory that can prompt for MFA when needed
                builder.Services.AddSingleton<IS3Service>(sp =>
                {
                    var cache = sp.GetRequiredService<IMemoryCache>();

                    var accessKey = builder.Configuration["AWS:AccessKey"];
                    var secretKey = builder.Configuration["AWS:SecretKey"];
                    var mfaArn = builder.Configuration["AWS:MFA"]; // ARN of MFA device

                    var prompter = sp.GetService<Dnp.S3.Browser.UI.Services.IMfaPrompter>();

                    bool mfaPrompted = false;
                    Func<Task<Amazon.S3.IAmazonS3>> factory = async () =>
                    {
                        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                        {
                            var baseCreds = new BasicAWSCredentials(accessKey, secretKey);

                            // If MFA ARN and prompter available, request MFA code and exchange for session token
                            if (!string.IsNullOrEmpty(mfaArn) && prompter != null && !mfaPrompted)
                            {
                                // Prompt only once per app session. If STS exchange fails, do not prompt again.
                                mfaPrompted = true;
                                try
                                {
                                    var code = await prompter.PromptForCodeAsync(mfaArn);
                                    if (!string.IsNullOrEmpty(code))
                                    {
                                        // Use AWS STS to get a session token via MFA
                                        var sts = new Amazon.SecurityToken.AmazonSecurityTokenServiceClient(baseCreds, regionEndpoint);
                                        var getReq = new Amazon.SecurityToken.Model.GetSessionTokenRequest { SerialNumber = mfaArn, TokenCode = code, DurationSeconds = 3600 };
                                        var getResp = await sts.GetSessionTokenAsync(getReq);
                                        var c = getResp.Credentials;
                                        var sessionCreds = new Amazon.Runtime.SessionAWSCredentials(c.AccessKeyId, c.SecretAccessKey, c.SessionToken);
                                        // store session credentials
                                        var store = sp.GetService<Dnp.S3.Browser.UI.Services.AwsSessionStore>();
                                        store?.SetCredentials(sessionCreds);
                                        return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(sessionCreds, regionEndpoint) : new Amazon.S3.AmazonS3Client(sessionCreds);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"MFA STS exchange failed: {ex.Message}");
                                    // fall through to return base creds and do not prompt again
                                }
                            }

                            // If a session already exists in the store, use it
                            var existingStore = sp.GetService<Dnp.S3.Browser.UI.Services.AwsSessionStore>();
                            var existing = existingStore?.GetCredentials();
                            if (existing != null)
                            {
                                return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(existing, regionEndpoint) : new Amazon.S3.AmazonS3Client(existing);
                            }

                            return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(baseCreds, regionEndpoint) : new Amazon.S3.AmazonS3Client(baseCreds);
                        }

                        return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(regionEndpoint) : new Amazon.S3.AmazonS3Client();
                    };

                    return new Dnp.S3.Browser.Services.Aws.AwsS3Service(factory, cache);
                });
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

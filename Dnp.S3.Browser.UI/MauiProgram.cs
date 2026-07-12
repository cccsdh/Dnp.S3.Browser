using Microsoft.Extensions.Logging;
using Dnp.S3.Browser.Core.Interfaces;
using Dnp.S3.Browser.Services.Local;
using Dnp.S3.Browser.ViewModels.ViewModels;
using Dnp.S3.Browser.UI.Pages;
using Dnp.S3.Browser.UI.Services;
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

            // Settings stored in SQLite (migrated from appsettings.json)
            builder.Services.AddSingleton<Dnp.S3.Browser.UI.Services.SettingsService>();
            // MFA prompter and session store used by AWS client factory
            builder.Services.AddSingleton<Dnp.S3.Browser.UI.Services.IMfaPrompter, Dnp.S3.Browser.UI.Services.MfaPrompter>();
            builder.Services.AddSingleton<Dnp.S3.Browser.UI.Services.AwsSessionStore>();

            // Register IS3Service to choose Local or AWS at runtime based on stored settings in SQLite.
            // The SettingsService is used to retrieve persisted settings (UseLocalS3, AWS credentials, MFA, Region).
            builder.Services.AddSingleton<IS3Service>(sp =>
            {
                var cache = sp.GetRequiredService<IMemoryCache>();
                var settingsSvc = sp.GetRequiredService<Dnp.S3.Browser.UI.Services.SettingsService>();
                var settings = settingsSvc.GetSettings();
                // If no settings exist, postpone showing the initial settings UI until after
                // the app window and page are created. CreateWindow will detect this and
                // invoke the page's PromptForSettingsIfMissing method on the UI thread.
                if (settings == null)
                {
                    StartupLog.Log("No default settings found; will prompt for initial settings after window creation.");
                }
                var useLocalSettings = settings?.UseLocalS3 ?? builder.Configuration.GetValue<bool?>("UseLocalS3") ?? false;

                if (useLocalSettings)
                {
                    var root = Path.Combine(FileSystem.AppDataDirectory, "LocalS3");
                    return new LocalS3Service(root, cache);
                }

                // AWS path
                string? region = settings?.Region ?? builder.Configuration["AWS:Region"];
                Amazon.RegionEndpoint? regionEndpoint = null;
                if (!string.IsNullOrEmpty(region))
                {
                    regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
                }

                // MFA prompter (UI) - used when explicit credentials are provided and MFA ARN present
                var prompter = sp.GetService<Dnp.S3.Browser.UI.Services.IMfaPrompter>();
                // Session store to cache created session credentials for app lifetime
                var sessionStore = sp.GetService<Dnp.S3.Browser.UI.Services.AwsSessionStore>();

                var accessKey = settings?.AccessKey ?? builder.Configuration["AWS:AccessKey"];
                var secretKey = settings?.SecretKey ?? builder.Configuration["AWS:SecretKey"];
                var mfaArn = settings?.Mfa ?? builder.Configuration["AWS:MFA"]; // ARN of MFA device

                bool mfaPrompted = false;
                Func<Task<Amazon.S3.IAmazonS3>> factory = async () =>
                {
                    if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                    {
                        var baseCreds = new BasicAWSCredentials(accessKey, secretKey);

                        // If we already have a valid session, use it
                        var existing = sessionStore?.GetCredentials();
                        if (existing != null)
                        {
                            return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(existing, regionEndpoint) : new Amazon.S3.AmazonS3Client(existing);
                        }

                        // Prompt for MFA only when configured and not already successfully prompted
                        if (!string.IsNullOrEmpty(mfaArn) && prompter != null && !mfaPrompted)
                        {
                            try
                            {
                                var code = await prompter.PromptForCodeAsync(mfaArn);
                                if (!string.IsNullOrEmpty(code))
                                {
                                    var sts = new Amazon.SecurityToken.AmazonSecurityTokenServiceClient(baseCreds, regionEndpoint);
                                    var getReq = new Amazon.SecurityToken.Model.GetSessionTokenRequest { SerialNumber = mfaArn, TokenCode = code, DurationSeconds = 3600 };
                                    var getResp = await sts.GetSessionTokenAsync(getReq);
                                    var c = getResp.Credentials;
                                    var sessionCreds = new Amazon.Runtime.SessionAWSCredentials(c.AccessKeyId, c.SecretAccessKey, c.SessionToken);
                                    // store session and mark as prompted only after successful exchange
                                    sessionStore?.SetCredentials(sessionCreds);
                                    mfaPrompted = true;
                                    return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(sessionCreds, regionEndpoint) : new Amazon.S3.AmazonS3Client(sessionCreds);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"MFA STS exchange failed: {ex.Message}");
                                // Do not set mfaPrompted here - allow future attempts to prompt again
                            }
                        }

                        // No session available (or MFA not completed) - fall back to base credentials
                        return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(baseCreds, regionEndpoint) : new Amazon.S3.AmazonS3Client(baseCreds);
                    }

                    return regionEndpoint != null ? new Amazon.S3.AmazonS3Client(regionEndpoint) : new Amazon.S3.AmazonS3Client();
                };

                return new Dnp.S3.Browser.Services.Aws.AwsS3Service(factory, cache);
            });

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

namespace Dnp.S3.Browser.UI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Ensure there is a MainPage available early so services that show UI during
            // initialization (e.g. blocking first-run settings) can use Application.Current.MainPage.
            Application.Current!.MainPage = new ContentPage();

            // Resolve the S3BrowserPage from the DI container and make it the app's main page.
            var services = this.Handler?.MauiContext?.Services;
            var pageObj = services?.GetService(typeof(Dnp.S3.Browser.UI.Pages.S3BrowserPage));
            var page = pageObj as Page;

            if (page == null)
            {
                // Fallback: construct with resolved viewmodel or a default LocalS3Service-backed VM
                var vm = services?.GetService(typeof(Dnp.S3.Browser.ViewModels.ViewModels.S3BrowserViewModel)) as Dnp.S3.Browser.ViewModels.ViewModels.S3BrowserViewModel;
                // Get IConfiguration from the resolved service provider (MauiContext.Services)
                var config = services?.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) as Microsoft.Extensions.Configuration.IConfiguration;
                page = new Dnp.S3.Browser.UI.Pages.S3BrowserPage(vm ?? new Dnp.S3.Browser.ViewModels.ViewModels.S3BrowserViewModel(new Dnp.S3.Browser.Services.Local.LocalS3Service(Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "LocalS3"), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))), config);
            }

            // Replace the temporary MainPage with the real navigation page.
            var nav = new NavigationPage(page);
            Application.Current!.MainPage = nav;
            // If settings are missing, ask the page to show the settings overlay once the
            // window and navigation page are ready. Do this on the main thread so UI
            // elements can be manipulated safely.
            try
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var services = this.Handler?.MauiContext?.Services;
                        var settingsSvc = services?.GetService(typeof(Dnp.S3.Browser.UI.Services.SettingsService)) as Dnp.S3.Browser.UI.Services.SettingsService;
                        if (settingsSvc == null) return;

                        // Only prompt if there is no default settings saved
                        if (settingsSvc.GetSettings() == null)
                        {
                            var spage = page as Dnp.S3.Browser.UI.Pages.S3BrowserPage;
                            if (spage != null)
                            {
                                Dnp.S3.Browser.UI.Services.StartupLog.Log("App.CreateWindow: invoking PromptForSettingsIfMissing on S3BrowserPage.");
                                await spage.PromptForSettingsIfMissing(settingsSvc);
                                Dnp.S3.Browser.UI.Services.StartupLog.Log("App.CreateWindow: PromptForSettingsIfMissing completed.");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Dnp.S3.Browser.UI.Services.StartupLog.Log($"App.CreateWindow: exception while prompting for settings: {ex}");
                    }
                });
            }
            catch { }
            return new Window(nav) { Title = "Dnp.S3.Browser.UI" };
        }
    }
}

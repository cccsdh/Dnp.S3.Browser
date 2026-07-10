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

            // Set Application.Current.MainPage so UI prompts (DisplayPromptAsync) have a MainPage to act on.
            var nav = new NavigationPage(page);
            Application.Current!.MainPage = nav;
            return new Window(nav) { Title = "Dnp.S3.Browser.UI" };
        }
    }
}

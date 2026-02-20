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
                page = new Dnp.S3.Browser.UI.Pages.S3BrowserPage(vm ?? new Dnp.S3.Browser.ViewModels.ViewModels.S3BrowserViewModel(new Dnp.S3.Browser.Services.Local.LocalS3Service(Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "LocalS3"), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))));
            }

            return new Window(new NavigationPage(page)) { Title = "Dnp.S3.Browser.UI" };
        }
    }
}

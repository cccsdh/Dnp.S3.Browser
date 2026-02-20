namespace Dnp.S3.Browser.UI
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnOpenS3BrowserClicked(object? sender, EventArgs e)
        {
            // Resolve S3BrowserPage from the DI container if available
            var services = this.Handler?.MauiContext?.Services;
            object? pageObj = services?.GetService(typeof(Dnp.S3.Browser.UI.Pages.S3BrowserPage));
            Page? page = pageObj as Page;

            if (page == null)
            {
                // Fallback: construct with resolved viewmodel or create default
                var vm = services?.GetService(typeof(Dnp.S3.Browser.ViewModels.ViewModels.S3BrowserViewModel)) as Dnp.S3.Browser.ViewModels.ViewModels.S3BrowserViewModel;
                page = new Dnp.S3.Browser.UI.Pages.S3BrowserPage(vm ?? new Dnp.S3.Browser.ViewModels.ViewModels.S3BrowserViewModel(new Dnp.S3.Browser.Services.Local.LocalS3Service(Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "LocalS3"), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))));
            }

            if (Navigation != null && page != null)
            {
                await Navigation.PushAsync(page);
            }
        }
    }
}

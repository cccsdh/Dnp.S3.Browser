#if WINDOWS
using System;
using System.Linq;
using System.Threading.Tasks;
using WinRT.Interop;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;

namespace Dnp.S3.Browser.UI.Platforms.Windows
{
    public static class WindowsFolderPicker
    {
        public static async Task<string?> PickFolderAsync()
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            // FolderPicker requires at least one file type filter
            picker.FileTypeFilter.Add("*");

            try
            {
                // Get the current MAUI window and its underlying native window
                var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows?.FirstOrDefault();
                if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    var hwnd = WindowNative.GetWindowHandle(nativeWindow);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }

                var folder = await picker.PickSingleFolderAsync();
                return folder?.Path;
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif

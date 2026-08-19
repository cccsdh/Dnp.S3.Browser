#if WINDOWS
using System;
using System.Linq;
using System.Threading.Tasks;
using WinRT.Interop;
using Microsoft.UI.Xaml;

namespace Dnp.S3.Browser.UI.Platforms.Windows
{
    public static class WindowsFolderPicker
    {
        public static Task<string?> PickFolderAsync()
        {
            var hwnd = IntPtr.Zero;
            var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows?.FirstOrDefault();
            if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                hwnd = WindowNative.GetWindowHandle(nativeWindow);
            }

            return Dnp.S3.Browser.WindowsDialogs.FolderPickerDialog.PickFolderAsync(hwnd);
        }
    }
}
#endif

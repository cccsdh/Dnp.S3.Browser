#if WINDOWS
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Microsoft.UI.Xaml;
using Microsoft.Maui.Controls;
using System;

namespace Dnp.S3.Browser.UI.Platforms.Windows
{
    public static class WindowsMfaDialog
    {
        public static async Task<string?> ShowAsync(string mfaArn)
        {
            try
            {
                // Obtain the current MAUI window and native WinUI Window
                var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows?.FirstOrDefault();
                if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    var dlg = new ContentDialog();
                    dlg.Title = "MFA Required";
                    dlg.IsPrimaryButtonEnabled = true;
                    dlg.PrimaryButtonText = "OK";
                    dlg.IsSecondaryButtonEnabled = true;
                    dlg.SecondaryButtonText = "Cancel";

                    var tb = new Microsoft.UI.Xaml.Controls.TextBox { Header = $"Enter MFA code for device: {mfaArn}", PlaceholderText = "123456" };
                    dlg.Content = tb;

                    // Attach to the window's XamlRoot so the dialog is modal to the app window
                    dlg.XamlRoot = nativeWindow.Content.XamlRoot;

                    var result = await dlg.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        return tb.Text;
                    }
                    return null;
                }
            }
            catch
            {
                // ignore and fallback
            }
            return null;
        }
    }
}
#endif

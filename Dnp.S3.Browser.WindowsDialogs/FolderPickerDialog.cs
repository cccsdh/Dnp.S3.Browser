using System.Windows.Forms;

namespace Dnp.S3.Browser.WindowsDialogs;

public static class FolderPickerDialog
{
    // Uses WinForms' FolderBrowserDialog rather than the WinRT Windows.Storage.Pickers.FolderPicker:
    // the WinRT picker is unreliable in unpackaged (WindowsPackageType=None) apps - it can fail COM
    // activation with an unhelpful/empty exception message even when a window handle is supplied.
    // Throws if the dialog itself fails to show; returns null only when the user cancels it.
    public static Task<string?> PickFolderAsync(IntPtr ownerHandle)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder",
            UseDescriptionForTitle = true,
        };

        var result = ownerHandle != IntPtr.Zero
            ? dialog.ShowDialog(new Win32WindowHandle(ownerHandle))
            : dialog.ShowDialog();
        var path = result == DialogResult.OK ? dialog.SelectedPath : null;
        return Task.FromResult(path);
    }

    private sealed class Win32WindowHandle : IWin32Window
    {
        public Win32WindowHandle(IntPtr handle) => Handle = handle;
        public IntPtr Handle { get; }
    }
}

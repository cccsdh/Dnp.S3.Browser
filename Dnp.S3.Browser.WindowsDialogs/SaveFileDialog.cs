using System.Windows.Forms;

namespace Dnp.S3.Browser.WindowsDialogs;

public static class SaveFileDialog
{
    // Uses WinForms' SaveFileDialog for the same reason FolderPickerDialog does: the WinRT
    // Windows.Storage.Pickers.FileSavePicker is unreliable in unpackaged (WindowsPackageType=None)
    // apps. Throws if the dialog itself fails to show; returns null only when the user cancels it.
    public static Task<string?> PickSaveFileAsync(IntPtr ownerHandle, string suggestedFileName)
    {
        using var dialog = new System.Windows.Forms.SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "All files (*.*)|*.*",
            OverwritePrompt = true,
        };

        var result = ownerHandle != IntPtr.Zero
            ? dialog.ShowDialog(new Win32WindowHandle(ownerHandle))
            : dialog.ShowDialog();
        var path = result == DialogResult.OK ? dialog.FileName : null;
        return Task.FromResult(path);
    }

    private sealed class Win32WindowHandle : IWin32Window
    {
        public Win32WindowHandle(IntPtr handle) => Handle = handle;
        public IntPtr Handle { get; }
    }
}

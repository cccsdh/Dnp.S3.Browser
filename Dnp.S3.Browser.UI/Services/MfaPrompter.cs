using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace Dnp.S3.Browser.UI.Services
{
    public class MfaPrompter : IMfaPrompter
    {
        public Task<string?> PromptForCodeAsync(string mfaArn)
        {
            var tcs = new TaskCompletionSource<string?>();

            try
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var main = Application.Current?.MainPage;
                        // Prefer a native WinUI dialog on Windows to avoid MAUI modal navigation issues
                        string? winResult = null;
#if WINDOWS
                        try
                        {
                            winResult = await Dnp.S3.Browser.UI.Platforms.Windows.WindowsMfaDialog.ShowAsync(mfaArn);
                        }
                        catch { winResult = null; }
#endif
                        if (!string.IsNullOrEmpty(winResult))
                        {
                            tcs.TrySetResult(winResult);
                        }
                        else if (main?.Navigation != null)
                        {
                            // Instead of modal navigation, attempt to show an inline popup by
                            // locating the current page's root grid and overlay (if present).
                            var currentPage = main.Navigation.NavigationStack?.LastOrDefault() ?? main.Navigation.NavigationStack?.FirstOrDefault();
                            if (currentPage != null)
                            {
                                try
                                {
                                    // Try to find a Grid named _rootLayout via reflection on the page's fields
                                    var field = currentPage.GetType().GetField("_rootLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                    var overlayField = currentPage.GetType().GetField("_overlay", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                    if (field != null && overlayField != null)
                                    {
                                        var root = field.GetValue(currentPage) as Microsoft.Maui.Controls.Grid;
                                        var overlay = overlayField.GetValue(currentPage) as Microsoft.Maui.Controls.Grid;
                                        if (root != null && overlay != null)
                                        {
                                            // Build popup UI
                                            var entry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "123456", WidthRequest = 200 };
                                            var ok = new Button { Text = "OK", WidthRequest = 80 };
                                            var cancel = new Button { Text = "Cancel", WidthRequest = 80 };
                                            var label = new Label { Text = $"Enter MFA code:", Margin = new Thickness(0,0,0,6) };
                                            var buttons = new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10, HorizontalOptions = LayoutOptions.Center, Children = { ok, cancel } };
                                            // Wrap the entry in a framed box so the code field is more noticeable
                                            var entryContainer = new Frame { Content = entry, Padding = new Thickness(6), CornerRadius = 6, HasShadow = false, BorderColor = Microsoft.Maui.Graphics.Color.FromArgb("#E0E0E0"), BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFFFFF"), HorizontalOptions = LayoutOptions.Center };

                                            // Build stack and apply app styles so popup matches main page
                                            var stack = new StackLayout { Padding = new Thickness(20), WidthRequest = 360, Children = { label, entryContainer, buttons } };
                                            try
                                            {
                                                var app = Application.Current;
                                                if (app != null && app.Resources != null)
                                                {
                                                    // Label style
                                                    if (app.Resources.ContainsKey("PrimaryLabel"))
                                                        label.Style = (Style)app.Resources["PrimaryLabel"];
                                                    else
                                                        label.SetDynamicResource(Label.TextColorProperty, "PrimaryTextColor");

                                                    // Entry style or dynamic resources
                                                    if (app.Resources.ContainsKey("DefaultEntry"))
                                                        entry.Style = (Style)app.Resources["DefaultEntry"];
                                                    else
                                                    {
                                                        entry.SetDynamicResource(Entry.TextColorProperty, "PrimaryTextColor");
                                                        entry.SetDynamicResource(Entry.BackgroundColorProperty, "InputBackgroundColor");
                                                    }

                                                    // Apply border/background colors to the entry container to match app theme
                                                    if (app.Resources.ContainsKey("BorderColor"))
                                                        entryContainer.BorderColor = (Microsoft.Maui.Graphics.Color)app.Resources["BorderColor"];
                                                    if (app.Resources.ContainsKey("InputBackgroundColor"))
                                                        entryContainer.SetDynamicResource(Frame.BackgroundColorProperty, "InputBackgroundColor");

                                                    // Button styles
                                                    if (app.Resources.ContainsKey("PrimaryButton"))
                                                    {
                                                        ok.Style = (Style)app.Resources["PrimaryButton"];
                                                        cancel.Style = (Style)app.Resources["PrimaryButton"];
                                                        // make icons/text fit
                                                        ok.Padding = new Thickness(6,4);
                                                        cancel.Padding = new Thickness(6,4);
                                                    }

                                                    // Stack/Frame background
                                                    stack.SetDynamicResource(VisualElement.BackgroundColorProperty, "SurfaceColor");
                                                }
                                                else
                                                {
                                                    stack.BackgroundColor = Colors.White;
                                                }
                                            }
                                            catch
                                            {
                                                stack.BackgroundColor = Colors.White;
                                            }

                                            // container in overlay center
                                            var container = new Frame { Content = stack, CornerRadius = 8, HasShadow = true, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

                                            // Show overlay
                                            overlay.IsVisible = true;

                                            // add container
                                            overlay.Children.Clear();
                                            overlay.Children.Add(container);

                                            // Handlers
                                            ok.Clicked += (_, __) => tcs.TrySetResult(entry.Text);
                                            cancel.Clicked += (_, __) => tcs.TrySetResult(null);

                                            // Handle Enter key on entry
                                            entry.Completed += (_, __) => tcs.TrySetResult(entry.Text);

                                            // Focus entry
                                            entry.Focus();

                                            var result = await tcs.Task;

                                            // Hide overlay
                                            overlay.IsVisible = false;
                                            overlay.Children.Clear();
                                            return;
                                        }
                                    }
                                }
                                catch { }
                            }

                            // Fallback to original modal approach if inline overlay not available
                            var entry2 = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "123456" };
                            var ok2 = new Button { Text = "OK" };
                            var cancel2 = new Button { Text = "Cancel" };
                            var label2 = new Label { Text = $"Enter MFA code for device: {mfaArn}", Margin = new Thickness(0,0,0,6) };

                            var entry2Container = new Frame { Content = entry2, Padding = new Thickness(6), CornerRadius = 6, HasShadow = false, BorderColor = Microsoft.Maui.Graphics.Color.FromArgb("#E0E0E0"), BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#FFFFFF"), HorizontalOptions = LayoutOptions.Center };

                            var buttons2 = new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 10, Children = { ok2, cancel2 } };
                            var stack2 = new StackLayout { Padding = new Thickness(20), Children = { label2, entry2Container, buttons2 } };

                            try
                            {
                                var app = Application.Current;
                                if (app != null && app.Resources != null)
                                {
                                    if (app.Resources.ContainsKey("PrimaryLabel"))
                                        label2.Style = (Style)app.Resources["PrimaryLabel"];
                                    else
                                        label2.SetDynamicResource(Label.TextColorProperty, "PrimaryTextColor");

                                    if (app.Resources.ContainsKey("DefaultEntry"))
                                        entry2.Style = (Style)app.Resources["DefaultEntry"];
                                    else
                                    {
                                        entry2.SetDynamicResource(Entry.TextColorProperty, "PrimaryTextColor");
                                        entry2.SetDynamicResource(Entry.BackgroundColorProperty, "InputBackgroundColor");
                                    }

                                    if (app.Resources.ContainsKey("BorderColor"))
                                        entry2Container.BorderColor = (Microsoft.Maui.Graphics.Color)app.Resources["BorderColor"];
                                    if (app.Resources.ContainsKey("InputBackgroundColor"))
                                        entry2Container.SetDynamicResource(Frame.BackgroundColorProperty, "InputBackgroundColor");

                                    if (app.Resources.ContainsKey("PrimaryButton"))
                                    {
                                        ok2.Style = (Style)app.Resources["PrimaryButton"];
                                        cancel2.Style = (Style)app.Resources["PrimaryButton"];
                                    }
                                }
                            }
                            catch { }

                            var page2 = new ContentPage { Content = stack2 };

                            ok2.Clicked += (_, __) => tcs.TrySetResult(entry2.Text);
                            cancel2.Clicked += (_, __) => tcs.TrySetResult(null);

                            await main.Navigation.PushModalAsync(page2, false);
                            entry2.Focus();

                            var result2 = await tcs.Task;
                            try { await main.Navigation.PopModalAsync(false); } catch { }
                            if (!tcs.Task.IsCompleted) tcs.TrySetResult(result2);
                        }
                        else
                        {
                            // fallback to DisplayPromptAsync
                            var title = "MFA Required";
                            var message = $"Enter MFA code for device: {mfaArn}";
                            var code = await Application.Current?.MainPage?.DisplayPromptAsync(title, message, "OK", "Cancel", keyboard: Keyboard.Numeric);
                            tcs.TrySetResult(code);
                        }
                    }
                    catch
                    {
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch
            {
                tcs.TrySetResult(null);
            }

            return tcs.Task;
        }
    }
}

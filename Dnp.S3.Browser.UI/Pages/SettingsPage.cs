using Microsoft.Maui.Controls;
using System;
using Dnp.S3.Browser.UI.Services;

namespace Dnp.S3.Browser.UI.Pages
{
    public class SettingsPage : ContentView
    {
        private readonly SettingsService _settingsSvc;
        private Entry _nameEntry;
        private Switch _useLocalSwitch;
        private Entry _accessEntry;
        private Entry _secretEntry;
        private Entry _mfaEntry;
        private Entry _regionEntry;
        private Button _saveBtn;
        private Switch _defaultSwitch;
        private SettingsModel? _existing;

        public event EventHandler? Saved;
        public event EventHandler? Cancelled;

        public SettingsPage(SettingsService settingsSvc, SettingsModel? existing = null)
        {
            _settingsSvc = settingsSvc;
            _existing = existing;

            // Create entries using the app's DefaultEntry style and primary text color
            _nameEntry = new Entry { Placeholder = "Account name (unique)", Style = (Style)Application.Current.Resources["DefaultEntry"] };
            _useLocalSwitch = new Switch { IsToggled = false };
            _accessEntry = new Entry { Placeholder = "AWS Access Key", Style = (Style)Application.Current.Resources["DefaultEntry"] };
            _secretEntry = new Entry { Placeholder = "AWS Secret Key", IsPassword = true, Style = (Style)Application.Current.Resources["DefaultEntry"] };
            _mfaEntry = new Entry { Placeholder = "MFA ARN (optional)", Style = (Style)Application.Current.Resources["DefaultEntry"] };
            _regionEntry = new Entry { Placeholder = "AWS Region (e.g. us-east-1)", Style = (Style)Application.Current.Resources["DefaultEntry"] };
            _defaultSwitch = new Switch { IsToggled = existing == null };

            // Icon-only action buttons to match main page style
            _saveBtn = new Button { Text = "💾", HorizontalOptions = LayoutOptions.End, Style = (Style)Application.Current.Resources["PrimaryButton"], WidthRequest = 36, HeightRequest = 36, FontSize = 20, BackgroundColor = Colors.Transparent };
            _saveBtn.Padding = new Thickness(0);
            _saveBtn.Clicked += OnSaveClicked;
            // Tooltip / accessibility
            try
            {
                Microsoft.Maui.Controls.AutomationProperties.SetHelpText(_saveBtn, "Save");
#if WINDOWS
                _saveBtn.HandlerChanged += (s, e) =>
                {
                    try
                    {
                        var native = _saveBtn.Handler?.PlatformView as global::Microsoft.UI.Xaml.FrameworkElement;
                        if (native != null)
                            global::Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(native, "Save");
                    }
                    catch { }
                };
#endif
            }
            catch { }

            var cancelBtn = new Button { Text = "✖️", HorizontalOptions = LayoutOptions.End, Style = (Style)Application.Current.Resources["PrimaryButton"], WidthRequest = 36, HeightRequest = 36, FontSize = 20, BackgroundColor = Colors.Transparent };
            cancelBtn.Padding = new Thickness(0);
            cancelBtn.Clicked += OnCancelClicked;
            try
            {
                Microsoft.Maui.Controls.AutomationProperties.SetHelpText(cancelBtn, "Cancel");
#if WINDOWS
                cancelBtn.HandlerChanged += (s, e) =>
                {
                    try
                    {
                        var native = cancelBtn.Handler?.PlatformView as global::Microsoft.UI.Xaml.FrameworkElement;
                        if (native != null)
                            global::Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(native, "Cancel");
                    }
                    catch { }
                };
#endif
            }
            catch { }

            if (existing != null)
            {
                // populate
                _nameEntry.Text = existing.Name;
                _useLocalSwitch.IsToggled = existing.UseLocalS3;
                _accessEntry.Text = existing.AccessKey;
                _secretEntry.Text = existing.SecretKey;
                _mfaEntry.Text = existing.Mfa;
                _regionEntry.Text = existing.Region;
                _defaultSwitch.IsToggled = existing.IsDefault;
            }

            var grid = new Grid { RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto } }, ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star } }, Padding = new Thickness(12) };

            int r = 0;
            // Use PrimaryLabel style for labels and wrap entries in a Frame to ensure visible border/background
            grid.Add(new Label { Text = "Name:", Style = (Style)Application.Current.Resources["PrimaryLabel"] }, 0, r);
            grid.Add(new Frame { Content = _nameEntry, Padding = new Thickness(6), CornerRadius = 6, BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["InputBackgroundColor"], BorderColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["BorderColor"] }, 1, r++);

            grid.Add(new Label { Text = "Use Local S3:", Style = (Style)Application.Current.Resources["PrimaryLabel"] }, 0, r);
            grid.Add(_useLocalSwitch, 1, r++);

            grid.Add(new Label { Text = "Access Key:", Style = (Style)Application.Current.Resources["PrimaryLabel"] }, 0, r);
            grid.Add(new Frame { Content = _accessEntry, Padding = new Thickness(6), CornerRadius = 6, BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["InputBackgroundColor"], BorderColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["BorderColor"] }, 1, r++);

            grid.Add(new Label { Text = "Secret Key:", Style = (Style)Application.Current.Resources["PrimaryLabel"] }, 0, r);
            grid.Add(new Frame { Content = _secretEntry, Padding = new Thickness(6), CornerRadius = 6, BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["InputBackgroundColor"], BorderColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["BorderColor"] }, 1, r++);

            grid.Add(new Label { Text = "MFA ARN:", Style = (Style)Application.Current.Resources["PrimaryLabel"] }, 0, r);
            grid.Add(new Frame { Content = _mfaEntry, Padding = new Thickness(6), CornerRadius = 6, BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["InputBackgroundColor"], BorderColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["BorderColor"] }, 1, r++);

            grid.Add(new Label { Text = "Region:", Style = (Style)Application.Current.Resources["PrimaryLabel"] }, 0, r);
            grid.Add(new Frame { Content = _regionEntry, Padding = new Thickness(6), CornerRadius = 6, BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["InputBackgroundColor"], BorderColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["BorderColor"] }, 1, r++);

            grid.Add(new Label { Text = "Set as default:", Style = (Style)Application.Current.Resources["PrimaryLabel"] }, 0, r);
            grid.Add(_defaultSwitch, 1, r++);

            var actions = new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { cancelBtn, _saveBtn } };
            var stack = new StackLayout { Children = { grid, actions }, Padding = new Thickness(12) };

            Content = new Frame { Content = stack, CornerRadius = 8, Padding = new Thickness(0), BackgroundColor = Colors.White };
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            var s = new SettingsModel
            {
                Name = _nameEntry.Text?.Trim(),
                IsDefault = _defaultSwitch.IsToggled,
                UseLocalS3 = _useLocalSwitch.IsToggled,
                AccessKey = string.IsNullOrWhiteSpace(_accessEntry.Text) ? null : _accessEntry.Text.Trim(),
                SecretKey = string.IsNullOrWhiteSpace(_secretEntry.Text) ? null : _secretEntry.Text.Trim(),
                Mfa = string.IsNullOrWhiteSpace(_mfaEntry.Text) ? null : _mfaEntry.Text.Trim(),
                Region = string.IsNullOrWhiteSpace(_regionEntry.Text) ? null : _regionEntry.Text.Trim()
            };

            if (_existing != null)
            {
                s.Id = _existing.Id;
            }

            // Basic validation
            if (string.IsNullOrEmpty(s.Name))
            {
                Application.Current?.MainPage?.DisplayAlert("Validation", "Please enter an account name.", "OK");
                return;
            }

            _settingsSvc.SaveSettings(s);
            Saved?.Invoke(this, EventArgs.Empty);
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}

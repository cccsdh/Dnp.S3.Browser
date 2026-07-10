using Dnp.S3.Browser.ViewModels.ViewModels;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Dnp.S3.Browser.Core.Models;
using Dnp.S3.Browser.UI.Converters;

namespace Dnp.S3.Browser.UI.Pages;

public partial class S3BrowserPage : ContentPage
{
    private readonly S3BrowserViewModel _vm;
    private CollectionView _bucketsView = null!;
    private CollectionView _objectsView = null!;
    private ObservableCollection<S3ObjectInfo> _filteredObjects = null!;
    private string? _filterText;
    private Entry? _filterEntry;
    private System.Threading.CancellationTokenSource? _filterCts;
    private StackLayout _breadcrumbLayout = null!;
    private Button _downloadBtn = null!;
    private Button _uploadBtn = null!;
    private Button _renameBtn = null!;
    private Button _deleteBtn = null!;
    private Grid _rootLayout = null!;
    private Grid _overlay = null!;
    private readonly bool _enableDebug;
    private void Log(string m)
    {
        if (_enableDebug)
            Debug.WriteLine(m);
    }

    public S3BrowserPage(S3BrowserViewModel vm, IConfiguration? config = null)
    {
        _vm = vm;
        _enableDebug = config?.GetValue<bool?>("S3Browser:EnableDebug") ?? false;
        BindingContext = _vm;


        // Breadcrumb layout
        _breadcrumbLayout = new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 4, Padding = new Thickness(6, 0) };

        // Buckets view
        _bucketsView = new CollectionView { SelectionMode = SelectionMode.Single };
        _bucketsView.SelectionChanged += async (s, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is S3BucketInfo b)
            {
                _vm.SelectedBucket = b;
                _vm.SelectedPrefix = null;
                UpdateBreadcrumb();
                // Load objects for the selected bucket automatically
                Log("BucketsView: starting LoadObjectsCommand (bucket selection)");
                await _vm.LoadObjectsCommand.ExecuteAsync(null);
                // VM marshals collection changes to the UI thread; no artificial delay required
                ApplyFilter();
            }
        };
        _bucketsView.ItemTemplate = new DataTemplate(() =>
        {
            var gridItem = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star } },
                Padding = new Thickness(10, 8)
            };
            var icon = new Label { Text = "🗂️", VerticalOptions = LayoutOptions.Center };
            icon.SetDynamicResource(Label.TextColorProperty, "PrimaryTextColor");
            var name = new Label { VerticalOptions = LayoutOptions.Center, Style = (Style)Application.Current.Resources["PrimaryLabel"] };
            name.SetBinding(Label.TextProperty, "Name");
            gridItem.Add(icon, 0, 0);
            gridItem.Add(name, 1, 0);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => { }; // placeholder to enable visual selection affordance
            gridItem.GestureRecognizers.Add(tap);
            // Add a bottom separator to simulate grid lines between rows
            var separator = new BoxView { HeightRequest = 1, BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#E0E0E0"), HorizontalOptions = LayoutOptions.FillAndExpand };
            return new StackLayout { Spacing = 0, Children = { gridItem, separator } };
        });

        // Objects view
        _objectsView = new CollectionView { SelectionMode = SelectionMode.Multiple };
        // When objects are selected, if exactly one folder is selected we drill into it; otherwise update action buttons
        _objectsView.SelectionChanged += async (s, e) =>
        {
            var selectedItems = e.CurrentSelection?.Cast<object>().Select(o => o as S3ObjectInfo).Where(x => x != null).Cast<S3ObjectInfo>().ToList() ?? new List<S3ObjectInfo>();
            Log($"ObjectsView: selection changed count={selectedItems.Count}");
            if (selectedItems.Count == 1 && selectedItems[0].IsFolder)
            {
                var selected = selectedItems[0];
                Log($"ObjectsView: drilling into folder={selected.Key}");
                // When drilling into a folder, clear any object filter so the new folder lists all items
                _filterText = null;
                if (_filterEntry != null)
                    _filterEntry.Text = string.Empty;

                _vm.SelectedPrefix = selected.Key;
                UpdateBreadcrumb();
                Log("ObjectsView: starting LoadObjectsCommand for folder");
                try
                {
                    await _vm.LoadObjectsCommand.ExecuteAsync(null);
                    Log("ObjectsView: LoadObjectsCommand for folder completed");
                    // VM marshals collection changes to the UI thread; no artificial delay required
                }
                catch (System.Exception ex)
                {
                    Log($"ObjectsView: LoadObjectsCommand for folder failed: {ex}");
                }
                ApplyFilter();
                UpdateActionButtons();
                // clear selection so the same folder can be clicked again
                if (_objectsView.SelectedItem != null)
                    _objectsView.SelectedItem = null;
                return;
            }

            // For multiple selection or single file selection, update action buttons
            UpdateActionButtons();
        };
        _objectsView.ItemTemplate = new DataTemplate(() =>
        {
            var gridItem = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } },
                Padding = new Thickness(10,8)
            };

            var icon = new Label { VerticalOptions = LayoutOptions.Center };
            icon.SetDynamicResource(Label.TextColorProperty, "PrimaryTextColor");
            icon.SetBinding(Label.TextProperty, new Binding("IsFolder", converter: new FolderIconConverter()));
            gridItem.Add(icon, 0, 0);

            var key = new Label { VerticalOptions = LayoutOptions.Center, Style = (Style)Application.Current.Resources["PrimaryLabel"] };
            key.SetBinding(Label.TextProperty, "Key");
            gridItem.Add(key, 1, 0);

            var size = new Label { VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End, WidthRequest = 120, HorizontalTextAlignment = TextAlignment.End, Style = (Style)Application.Current.Resources["PrimaryLabel"] };
            size.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");
            size.SetBinding(Label.TextProperty, "Size");
            gridItem.Add(size, 2, 0);

            // Add selection tap for visual feedback
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => { }; // placeholder
            gridItem.GestureRecognizers.Add(tap);
            // Add a bottom separator to simulate grid lines between rows
            var separator = new BoxView { HeightRequest = 1, BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#E0E0E0"), HorizontalOptions = LayoutOptions.FillAndExpand };
            return new StackLayout { Spacing = 0, Children = { gridItem, separator } };
        });

        // Filtered collection and binding
        _filteredObjects = new ObservableCollection<S3ObjectInfo>();
        _objectsView.ItemsSource = _filteredObjects;
        // Only run filtering when the user has entered a filter. When no filter text is active
        // bind directly to the viewmodel collection to avoid repeated snapshotting and rebinding
        // while the VM is populating a large object list.
        _vm.Objects.CollectionChanged += (s, ev) => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (string.IsNullOrEmpty(_filterText))
            {
                // Direct bind for best performance during initial loads and VM population
                _objectsView.ItemsSource = _vm.Objects;
                _filteredObjects.Clear();
                Log($"Objects.CollectionChanged: direct-bound VM collection count={_vm.Objects.Count}");
                UpdateActionButtons();
                return;
            }

            // Otherwise apply the active filter
            ApplyFilter(_filterText);
        });

        // Action buttons (icon-only) with hover tooltips
        // Use an emoji inbox tray which renders consistently across platforms for download
        _downloadBtn = new Button { Text = "📥", IsEnabled = false, WidthRequest = 36, HeightRequest = 36, FontSize = 20, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _downloadBtn.Clicked += OnDownloadClicked;
        _uploadBtn = new Button { Text = "📤", IsEnabled = false, WidthRequest = 36, HeightRequest = 36, FontSize = 20, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _uploadBtn.Clicked += OnUploadClicked;
        _renameBtn = new Button { Text = "✏️", IsEnabled = false, WidthRequest = 36, HeightRequest = 36, FontSize = 20, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _renameBtn.Clicked += OnRenameClicked;
        _deleteBtn = new Button { Text = "🗑️", IsEnabled = false, WidthRequest = 36, HeightRequest = 36, FontSize = 20, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _deleteBtn.Clicked += OnDeleteClicked;

        // Attach tooltips (hover text) and accessibility help text
        AttachToolTip(_downloadBtn, "Download");
        AttachToolTip(_uploadBtn, "Upload");
        AttachToolTip(_renameBtn, "Rename");
        AttachToolTip(_deleteBtn, "Delete");

        // Bind buckets and initialize filter
        _bucketsView.ItemsSource = _vm.Buckets;
        ApplyFilter();

        // Layout grid - give the object pane more space than buckets
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, new RowDefinition { Height = GridLength.Auto } },
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = new GridLength(0.35, GridUnitType.Star) }, new ColumnDefinition { Width = new GridLength(0.65, GridUnitType.Star) } },
            Padding = new Thickness(10)
        };

        // Breadcrumb
        var breadcrumbScroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _breadcrumbLayout, HorizontalOptions = LayoutOptions.StartAndExpand };
        grid.Add(breadcrumbScroll, 0, 0);
        Grid.SetColumnSpan(breadcrumbScroll, 2);

        // Headers area: left = bucket header, right = filter + objects header
        var bucketHeader = new Label { Text = "Buckets", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(8, 6) };

        // Filter entry with overlay placeholder label
        _filterEntry = new Entry { Placeholder = string.Empty, HorizontalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Colors.White, Margin = new Thickness(0,0,0,4) };
        var filterOverlayLabel = new Label { Text = "[Enter Filter Text]", VerticalOptions = LayoutOptions.Center, Margin = new Thickness(6,0,0,4), IsVisible = true };
        filterOverlayLabel.SetDynamicResource(Label.TextColorProperty, "SecondaryTextColor");

        void UpdateFilterOverlayVisibility()
        {
            var hasText = !string.IsNullOrEmpty(_filterEntry.Text);
            // Hide overlay when entry has text or is focused
            filterOverlayLabel.IsVisible = !hasText && !_filterEntry.IsFocused;
        }

        _filterEntry.TextChanged += (s, e) => { _filterText = e.NewTextValue; ApplyFilter(e.NewTextValue); UpdateFilterOverlayVisibility(); };
        _filterEntry.Focused += (s, e) => UpdateFilterOverlayVisibility();
        _filterEntry.Unfocused += (s, e) => UpdateFilterOverlayVisibility();
        // Tap overlay to focus the entry
        var overlayTap = new TapGestureRecognizer();
        overlayTap.Tapped += (s, e) => { _filterEntry.Focus(); };
        filterOverlayLabel.GestureRecognizers.Add(overlayTap);

        // Place Entry and overlay label in a grid so label overlays the entry
        var filterGridOverlay = new Grid { HorizontalOptions = LayoutOptions.FillAndExpand };
        filterGridOverlay.Children.Add(_filterEntry);
        filterGridOverlay.Children.Add(filterOverlayLabel);

        // Ensure overlay visibility initial state
        UpdateFilterOverlayVisibility();

        var objectsHeaderGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } }, Padding = new Thickness(8, 6) };
        objectsHeaderGrid.Add(new Label { Text = string.Empty, VerticalOptions = LayoutOptions.Center }, 0, 0);
        objectsHeaderGrid.Add(new Label { Text = "Objects", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start }, 1, 0);
        objectsHeaderGrid.Add(new Label { Text = "Size", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End }, 2, 0);

        var objectsHeaderStack = new StackLayout { Orientation = StackOrientation.Vertical, Spacing = 6, Padding = new Thickness(6,4) };
        objectsHeaderStack.Add(filterGridOverlay);
        objectsHeaderStack.Add(objectsHeaderGrid);

        grid.Add(bucketHeader, 0, 1);
        grid.Add(objectsHeaderStack, 1, 1);

        // Put lists in frames for a cleaner, professional look
        var bucketsFrame = new Frame { Content = _bucketsView, Style = (Style)Application.Current.Resources["ItemFrame"] };
        var objectsFrame = new Frame { Content = _objectsView, Style = (Style)Application.Current.Resources["ItemFrame"] };

        // Content row
        grid.Add(bucketsFrame, 0, 2);
        grid.Add(objectsFrame, 1, 2);

        var bottomStack = new StackLayout { Orientation = StackOrientation.Horizontal, Padding = new Thickness(10), Spacing = 10, HorizontalOptions = LayoutOptions.End };
        _downloadBtn.CornerRadius = 6; _uploadBtn.CornerRadius = 6; _renameBtn.CornerRadius = 6; _deleteBtn.CornerRadius = 6;
        // Apply button style if present, then override padding so icon fits
        if (Application.Current?.Resources?.ContainsKey("PrimaryButton") == true)
        {
            _downloadBtn.Style = (Style)Application.Current.Resources["PrimaryButton"];
            _uploadBtn.Style = (Style)Application.Current.Resources["PrimaryButton"];
            _renameBtn.Style = (Style)Application.Current.Resources["PrimaryButton"];
            _deleteBtn.Style = (Style)Application.Current.Resources["PrimaryButton"];
        }
        // Override padding applied by the shared style so icons are not truncated
        _downloadBtn.Padding = new Thickness(0);
        _uploadBtn.Padding = new Thickness(0);
        _renameBtn.Padding = new Thickness(0);
        _deleteBtn.Padding = new Thickness(0);
        bottomStack.Add(_downloadBtn); bottomStack.Add(_uploadBtn); bottomStack.Add(_renameBtn); bottomStack.Add(_deleteBtn);
        grid.Add(bottomStack, 0, 3);
        Grid.SetColumnSpan(bottomStack, 2);

        // Root layout allows overlaying popups
        _rootLayout = new Grid();
        _rootLayout.Children.Add(grid);

        // Overlay for inline popups (hidden by default)
        _overlay = new Grid { BackgroundColor = new Microsoft.Maui.Graphics.Color(0f, 0f, 0f, 0.4f), IsVisible = false, InputTransparent = false };
        // Centered container for popup content
        var overlayContainer = new Grid { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _overlay.Children.Add(overlayContainer);
        _rootLayout.Children.Add(_overlay);

        Content = _rootLayout;
    }

    // Attach a simple tooltip for desktop platforms by setting AutomationProperties.HelpText
    // and handling pointer events on platforms that support hover (Windows). For other
    // platforms AutomationProperties.HelpText improves accessibility.
    private void AttachToolTip(Button btn, string text)
    {
        // Accessibility/help text
        Microsoft.Maui.Controls.AutomationProperties.SetHelpText(btn, text);

#if WINDOWS
        // On Windows attach a native tooltip to the platform control when the handler becomes available.
        btn.HandlerChanged += (s, e) =>
        {
            try
            {
                var native = btn.Handler?.PlatformView as global::Microsoft.UI.Xaml.FrameworkElement;
                if (native != null)
                {
                    global::Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(native, text);
                }
            }
            catch { }
        };
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Load buckets automatically when the page appears
        await _vm.LoadBucketsCommand.ExecuteAsync(null);
        // Ensure the buckets view is populated
        _bucketsView.ItemsSource = _vm.Buckets;
        UpdateBreadcrumb();
        UpdateActionButtons();
    }

    private void UpdateBreadcrumb()
    {
        _breadcrumbLayout.Children.Clear();
        if (_vm.SelectedBucket == null)
        {
            _breadcrumbLayout.Children.Add(new Label { Text = "No bucket selected", VerticalOptions = LayoutOptions.Center });
            UpdateActionButtons();
            return;
        }

        // Build clickable breadcrumb buttons: bucket -> segments
        _breadcrumbLayout.Children.Clear();

        // Bucket button
        var bucketBtn = new Button { Text = _vm.SelectedBucket.Name + "/", BackgroundColor = Colors.Transparent };
        bucketBtn.Clicked += async (_, __) => { _vm.SelectedPrefix = null; await _vm.LoadObjectsCommand.ExecuteAsync(null); ApplyFilter(); UpdateBreadcrumb(); };
        _breadcrumbLayout.Add(bucketBtn);

        var prefix = _vm.SelectedPrefix;
        if (string.IsNullOrEmpty(prefix)) { UpdateActionButtons(); return; }

        // Trim trailing slash and split
        var p = prefix.TrimEnd('/');
        var segments = p.Split('/');
        var cum = string.Empty;
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            cum = string.IsNullOrEmpty(cum) ? seg + "/" : cum + seg + "/";

            var segBtn = new Button { Text = seg + "/", BackgroundColor = Colors.Transparent };
            var targetPrefix = cum; // capture
            segBtn.Clicked += async (_, __) => { _vm.SelectedPrefix = targetPrefix; await _vm.LoadObjectsCommand.ExecuteAsync(null); ApplyFilter(); UpdateBreadcrumb(); };
            _breadcrumbLayout.Add(new Label { Text = "> ", VerticalOptions = LayoutOptions.Center });
            _breadcrumbLayout.Add(segBtn);
        }

        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        // Upload enabled when a bucket is selected
        _uploadBtn.IsEnabled = _vm.SelectedBucket != null;

        // Download/Rename/Delete enabled based on SelectedItems (supports multi-selection)
        var sels = _objectsView?.SelectedItems?.Cast<object>().Select(o => o as S3ObjectInfo).Where(x => x != null).Cast<S3ObjectInfo>().ToList() ?? new List<S3ObjectInfo>();
        var anySelected = sels.Any();
        var anyFileSelected = sels.Any(x => !x.IsFolder);
        _downloadBtn.IsEnabled = anyFileSelected;
        _renameBtn.IsEnabled = (sels.Count == 1 && !sels[0].IsFolder);
        _deleteBtn.IsEnabled = anySelected; // allow delete for files or folders when explicitly selected
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        if (_vm.SelectedBucket == null) return;

        var sels = _objectsView.SelectedItems?.Cast<object>().Select(o => o as S3ObjectInfo).Where(x => x != null).Cast<S3ObjectInfo>().ToList() ?? new List<S3ObjectInfo>();
        var files = sels.Where(s => !s.IsFolder).ToList();
        if (!files.Any())
        {
            await DisplayAlertAsync("Download", "No files selected to download.", "OK");
            return;
        }

        if (files.Count == 1)
        {
            var selected = files[0];
            var file = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Save to" });
            if (file == null) return;
            var localPath = Path.Combine(FileSystem.AppDataDirectory, file.FileName);
            await _vm.DownloadObjectAsync(_vm.SelectedBucket.Name, selected.Key, localPath);
            await DisplayAlertAsync("Downloaded", $"Saved to {localPath}", "OK");
            return;
        }

        // Multiple files: allow the user to pick a target folder on Windows, otherwise use AppData Downloads
#if WINDOWS
        var targetFolder = await Dnp.S3.Browser.UI.Platforms.Windows.WindowsFolderPicker.PickFolderAsync();
        if (string.IsNullOrEmpty(targetFolder))
        {
            await DisplayAlertAsync("Download", "No folder selected.", "OK");
            return;
        }
        var targetDir = targetFolder;
#else
        var targetDir = Path.Combine(FileSystem.AppDataDirectory, "Downloads", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
#endif
        Directory.CreateDirectory(targetDir);
        foreach (var f in files)
        {
            var fileName = Path.GetFileName(f.Key.Replace('/', Path.DirectorySeparatorChar));
            var localPath = Path.Combine(targetDir, fileName);
            await _vm.DownloadObjectAsync(_vm.SelectedBucket.Name, f.Key, localPath);
        }
        await DisplayAlertAsync("Downloaded", $"Saved {files.Count} files to {targetDir}", "OK");
    }

    private async void OnUploadClicked(object? sender, EventArgs e)
    {
        var pick = await FilePicker.PickMultipleAsync();
        if (pick == null || !pick.Any() || _vm.SelectedBucket == null) return;
        var paths = pick.Select(p => p.FullPath ?? string.Empty).Where(p => !string.IsNullOrEmpty(p));
        await _vm.UploadFilesAsync(_vm.SelectedBucket.Name, _vm.SelectedPrefix ?? string.Empty, paths);
        await DisplayAlertAsync("Upload", "Upload complete", "OK");
    }

    private async void OnRenameClicked(object? sender, EventArgs e)
    {
        var sels = _objectsView.SelectedItems?.Cast<object>().Select(o => o as S3ObjectInfo).Where(x => x != null).Cast<S3ObjectInfo>().ToList() ?? new List<S3ObjectInfo>();
        if (sels.Count != 1 || _vm.SelectedBucket == null) return;
        var sel = sels[0];
        if (sel.IsFolder) return;
        var result = await DisplayPromptAsync("Rename", "New key:", "OK", "Cancel", sel.Key);
        if (string.IsNullOrEmpty(result)) return;
        var confirm = await DisplayAlertAsync("Confirm", "Rename selected item?", "Yes", "No");
        if (!confirm) return;
        await _vm.RenameAsync(_vm.SelectedBucket.Name, sel.Key, result);
        await _vm.LoadObjectsCommand.ExecuteAsync(null);
        ApplyFilter();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_vm.SelectedBucket == null) return;
        var sels = _objectsView.SelectedItems?.Cast<object>().Select(o => o as S3ObjectInfo).Where(x => x != null).Cast<S3ObjectInfo>().ToList() ?? new List<S3ObjectInfo>();
        if (!sels.Any()) return;
        var confirm = await DisplayAlertAsync("Confirm", $"Delete {sels.Count} selected item(s)?", "Yes", "No");
        if (!confirm) return;
        var tasks = sels.Select(s => _vm.DeleteAsync(_vm.SelectedBucket.Name, s.Key, s.IsFolder));
        await Task.WhenAll(tasks);
        await _vm.LoadObjectsCommand.ExecuteAsync(null);
        ApplyFilter();
    }

    private void ApplyFilter(string? text = null)
    {
        // cancel any in-progress filter work
        try
        {
            _filterCts?.Cancel();
        }
        catch { }

        _filterCts = new System.Threading.CancellationTokenSource();
        var token = _filterCts.Token;
        text ??= _filterText;

        // Run filter asynchronously in batches to keep UI responsive for large lists
        _ = RunFilterAsync(text, token);
    }

    private async Task RunFilterAsync(string? text, System.Threading.CancellationToken token)
    {
        var items = _vm.Objects.ToList();
                Log($"RunFilterAsync: snapshot count={items.Count} filter='{text}'");

        // Determine target result set (apply predicate on background thread)
        List<S3ObjectInfo> matches;
        if (string.IsNullOrEmpty(text))
        {
            matches = items;
        }
        else
        {
            var t = text.ToLowerInvariant();
            matches = items.Where(o => !string.IsNullOrEmpty(o.Key) && o.Key.ToLowerInvariant().Contains(t)).ToList();
        }

        // If no filter text, bind directly to the viewmodel's collection for fastest initial display
            if (string.IsNullOrEmpty(text))
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _objectsView.ItemsSource = _vm.Objects;
                    _filteredObjects.Clear();
                    Log($"RunFilterAsync: bound objects view directly to VM collection count={_vm.Objects.Count}");
                    UpdateActionButtons();
                });
                return;
            }

        const int batchSize = 100;
        // Ensure CollectionView is using the filtered collection
        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            _objectsView.ItemsSource = _filteredObjects;
            _filteredObjects.Clear();
        });

        for (int i = 0; i < matches.Count; i += batchSize)
        {
            if (token.IsCancellationRequested) return;
            var batch = matches.Skip(i).Take(batchSize).ToList();
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var o in batch) _filteredObjects.Add(o);
                UpdateActionButtons();
            });

            Log($"RunFilterAsync: added batch start={i} count={batch.Count}");

            // let UI update (no artificial delay)
        }

        Log($"RunFilterAsync: completed filtered count={_filteredObjects.Count}");
    }
}

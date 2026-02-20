using Dnp.S3.Browser.ViewModels.ViewModels;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Caching.Memory;
using System.IO;
using Dnp.S3.Browser.UI.Converters;

namespace Dnp.S3.Browser.UI.Pages;

public partial class S3BrowserPage : ContentPage
{
    private readonly S3BrowserViewModel _vm;
    private CollectionView _bucketsView = null!;
    private CollectionView _objectsView = null!;
    private StackLayout _breadcrumbLayout = null!;
    private Button _downloadBtn = null!;
    private Button _uploadBtn = null!;
    private Button _renameBtn = null!;
    private Button _deleteBtn = null!;

    public S3BrowserPage(S3BrowserViewModel vm)
    {
        _vm = vm;
        BindingContext = _vm;

        // Breadcrumb layout (clickable segments)
        _breadcrumbLayout = new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 4, Padding = new Thickness(6, 0) };

        // Clicking an object item will drill into it if it's a folder. Selection UI is not needed.

        _bucketsView = new CollectionView { SelectionMode = SelectionMode.Single };
        _bucketsView.SelectionChanged += async (s, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is Dnp.S3.Browser.Core.Models.S3BucketInfo b)
            {
                _vm.SelectedBucket = b;
                _vm.SelectedPrefix = null;
                UpdateBreadcrumb();
                // Load objects for the selected bucket automatically
                await _vm.LoadObjectsCommand.ExecuteAsync(null);
                _objectsView.ItemsSource = _vm.Objects;
            }
        };
        _bucketsView.ItemTemplate = new DataTemplate(() =>
        {
            var gridItem = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
                Padding = 10
            };
            var icon = new Label { Text = "🗂️", VerticalOptions = LayoutOptions.Center };
            gridItem.Add(icon, 0, 0);
            var name = new Label { VerticalOptions = LayoutOptions.Center };
            name.SetBinding(Label.TextProperty, "Name");
            gridItem.Add(name, 1, 0);
            return gridItem;
        });

        _objectsView = new CollectionView { SelectionMode = SelectionMode.Single };
        // When an object is clicked/selected, drill into it if it's a folder
        _objectsView.SelectionChanged += async (s, e) =>
        {
            var selected = e.CurrentSelection.FirstOrDefault() as Dnp.S3.Browser.Core.Models.S3ObjectInfo;
            if (selected != null && selected.IsFolder)
            {
                _vm.SelectedPrefix = selected.Key;
                UpdateBreadcrumb();
                await _vm.LoadObjectsCommand.ExecuteAsync(null);
                _objectsView.ItemsSource = _vm.Objects;
                UpdateActionButtons();
            }
            else
            {
                // If a file is selected, update action buttons to allow download/rename/delete
                UpdateActionButtons();
            }
            // clear selection for folders (already handled) so the same item can be clicked again
            if (selected != null && selected.IsFolder && _objectsView.SelectedItem != null)
                _objectsView.SelectedItem = null;
        };

        _objectsView.ItemTemplate = new DataTemplate(() =>
        {
            var gridItem = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto } },
                Padding = 10
            };

            var icon = new Label { VerticalOptions = LayoutOptions.Center };
            icon.SetBinding(Label.TextProperty, new Binding("IsFolder", converter: new FolderIconConverter()));
            gridItem.Add(icon, 0, 0);

            var key = new Label { VerticalOptions = LayoutOptions.Center };
            key.SetBinding(Label.TextProperty, "Key");
            gridItem.Add(key, 1, 0);

            var size = new Label { VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
            size.SetBinding(Label.TextProperty, "Size");
            gridItem.Add(size, 2, 0);

            return gridItem;
        });

        _downloadBtn = new Button { Text = "Download", IsEnabled = false };
        _downloadBtn.Clicked += OnDownloadClicked;
        _uploadBtn = new Button { Text = "Upload", IsEnabled = false };
        _uploadBtn.Clicked += OnUploadClicked;
        _renameBtn = new Button { Text = "Rename", IsEnabled = false };
        _renameBtn.Clicked += OnRenameClicked;
        _deleteBtn = new Button { Text = "Delete", IsEnabled = false };
        _deleteBtn.Clicked += OnDeleteClicked;

        // Bind the CollectionViews to the viewmodel collections so UI updates automatically
        _bucketsView.ItemsSource = _vm.Buckets;
        _objectsView.ItemsSource = _vm.Objects;

        var grid = new Grid
        {
            // row 0: breadcrumb, row 1: headers, row 2: content, row 3: actions
            RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, new RowDefinition { Height = GridLength.Auto } },
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Star } }
        };

        // Top-left: breadcrumb (inside horizontal scroll)
        var breadcrumbScroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _breadcrumbLayout, HorizontalOptions = LayoutOptions.StartAndExpand };
        grid.Add(breadcrumbScroll, 0, 0);
        Grid.SetColumnSpan(breadcrumbScroll, 2);

        // Headers row
        var bucketHeader = new Label { Text = "Buckets", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(8,6) };
        // Objects header should align with the object item template columns (icon, name, size).
        var objectsHeaderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Padding = new Thickness(8,6)
        };
        // empty placeholder for icon column
        objectsHeaderGrid.Add(new Label { Text = string.Empty, VerticalOptions = LayoutOptions.Center }, 0, 0);
        // Objects label aligned to the left over the name column
        objectsHeaderGrid.Add(new Label { Text = "Objects", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start }, 1, 0);
        // Size label for the size column
        objectsHeaderGrid.Add(new Label { Text = "Size", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End }, 2, 0);

        grid.Add(bucketHeader, 0, 1);
        grid.Add(objectsHeaderGrid, 1, 1);

        // (No right-top controls)

        grid.Add(_bucketsView, 0, 2);
        grid.Add(_objectsView, 1, 2);

        var bottomStack = new StackLayout { Orientation = StackOrientation.Horizontal, Padding = 10, Spacing = 10 };
        bottomStack.Add(_downloadBtn);
        bottomStack.Add(_uploadBtn);
        bottomStack.Add(_renameBtn);
        bottomStack.Add(_deleteBtn);
        grid.Add(bottomStack, 0, 3);
        Grid.SetColumnSpan(bottomStack, 2);

        Content = grid;
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
        if (_vm.SelectedBucket == null)
        {
            _breadcrumbLayout.Children.Clear();
            _breadcrumbLayout.Children.Add(new Label { Text = "No bucket selected", VerticalOptions = LayoutOptions.Center });
            UpdateActionButtons();
            return;
        }

        // Build clickable breadcrumb buttons: bucket -> segments
        _breadcrumbLayout.Children.Clear();

        // Bucket button
        var bucketBtn = new Button { Text = _vm.SelectedBucket.Name + "/", BackgroundColor = Colors.Transparent };
        bucketBtn.Clicked += async (_, __) =>
        {
            _vm.SelectedPrefix = null;
            await _vm.LoadObjectsCommand.ExecuteAsync(null);
            _objectsView.ItemsSource = _vm.Objects;
            UpdateBreadcrumb();
        };
        _breadcrumbLayout.Add(bucketBtn);

        var prefix = _vm.SelectedPrefix;
        if (string.IsNullOrEmpty(prefix))
        {
            UpdateActionButtons();
            return;
        }

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
            segBtn.Clicked += async (_, __) =>
            {
                _vm.SelectedPrefix = targetPrefix;
                await _vm.LoadObjectsCommand.ExecuteAsync(null);
                _objectsView.ItemsSource = _vm.Objects;
                UpdateBreadcrumb();
            };
            _breadcrumbLayout.Add(new Label { Text = "> ", VerticalOptions = LayoutOptions.Center });
            _breadcrumbLayout.Add(segBtn);
        }

        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        // Upload enabled when a bucket is selected
        _uploadBtn.IsEnabled = _vm.SelectedBucket != null;

        // Download/Rename/Delete enabled when a file is selected
        var sel = _objectsView?.SelectedItem as Dnp.S3.Browser.Core.Models.S3ObjectInfo;
        var fileSelected = sel != null && !sel.IsFolder;
        _downloadBtn.IsEnabled = fileSelected;
        _renameBtn.IsEnabled = fileSelected;
        _deleteBtn.IsEnabled = sel != null; // allow delete for files or folders when explicitly selected
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        var selected = _objectsView.SelectedItem as Dnp.S3.Browser.Core.Models.S3ObjectInfo;
        if (selected == null || _vm.SelectedBucket == null) return;

        var file = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Save to" });
        if (file == null) return;

        var localPath = Path.Combine(FileSystem.AppDataDirectory, file.FileName);
        await _vm.DownloadObjectAsync(_vm.SelectedBucket.Name, selected.Key, localPath);
        await DisplayAlertAsync("Downloaded", $"Saved to {localPath}", "OK");
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
        var sel = _objectsView.SelectedItem as Dnp.S3.Browser.Core.Models.S3ObjectInfo;
        if (sel == null || _vm.SelectedBucket == null) return;
        var result = await DisplayPromptAsync("Rename", "New key:", "OK", "Cancel", sel.Key);
        if (string.IsNullOrEmpty(result)) return;
        var confirm = await DisplayAlertAsync("Confirm", "Rename selected item?", "Yes", "No");
        if (!confirm) return;
        await _vm.RenameAsync(_vm.SelectedBucket.Name, sel.Key, result);
        await _vm.LoadObjectsCommand.ExecuteAsync(null);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var sel = _objectsView.SelectedItem as Dnp.S3.Browser.Core.Models.S3ObjectInfo;
        if (sel == null || _vm.SelectedBucket == null) return;
        var confirm = await DisplayAlertAsync("Confirm", "Delete selected item?", "Yes", "No");
        if (!confirm) return;
        await _vm.DeleteAsync(_vm.SelectedBucket.Name, sel.Key, sel.IsFolder);
        await _vm.LoadObjectsCommand.ExecuteAsync(null);
    }
}

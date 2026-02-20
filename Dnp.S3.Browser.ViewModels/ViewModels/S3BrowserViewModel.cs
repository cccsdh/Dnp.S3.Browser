using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dnp.S3.Browser.Core.Interfaces;
using Dnp.S3.Browser.Core.Models;
using System.Collections.ObjectModel;

namespace Dnp.S3.Browser.ViewModels.ViewModels;

public class S3BrowserViewModel : ObservableObject
{
    private readonly IS3Service _s3;

    public ObservableCollection<S3BucketInfo> Buckets { get; } = new();
    public ObservableCollection<S3ObjectInfo> Objects { get; } = new();

    private S3BucketInfo? _selectedBucket;
    public S3BucketInfo? SelectedBucket
    {
        get => _selectedBucket;
        set => SetProperty(ref _selectedBucket, value);
    }

    private string? _selectedPrefix;
    public string? SelectedPrefix
    {
        get => _selectedPrefix;
        set => SetProperty(ref _selectedPrefix, value);
    }

    public IAsyncRelayCommand LoadBucketsCommand { get; }
    public IAsyncRelayCommand LoadObjectsCommand { get; }

    public S3BrowserViewModel(IS3Service s3)
    {
        _s3 = s3;
        LoadBucketsCommand = new AsyncRelayCommand(LoadBucketsAsync);
        LoadObjectsCommand = new AsyncRelayCommand(LoadObjectsAsync);
    }

    private async Task LoadBucketsAsync()
    {
        Buckets.Clear();
        var buckets = await _s3.ListBucketsAsync();
        foreach (var b in buckets) Buckets.Add(b);
    }

    private async Task LoadObjectsAsync()
    {
        if (SelectedBucket == null) return;
        Objects.Clear();
        var objs = await _s3.ListObjectsAsync(SelectedBucket.Name, SelectedPrefix);
        foreach (var o in objs) Objects.Add(o);
    }

    // The following methods are left as async methods to be invoked by the UI or commands you add there.
    public async Task DownloadObjectAsync(string bucketName, string key, string localPath)
    {
        using var fs = File.Create(localPath);
        await _s3.DownloadObjectAsync(bucketName, key, fs);
    }

    public async Task DownloadFolderAsync(string bucketName, string prefix, string localZipPath)
    {
        using var s = await _s3.DownloadFolderAsZipAsync(bucketName, prefix);
        using var fs = File.Create(localZipPath);
        await s.CopyToAsync(fs);
    }

    public async Task UploadFilesAsync(string bucketName, string folderPrefix, IEnumerable<string> filePaths)
    {
        var inputs = filePaths.Select(p => (fileName: Path.GetFileName(p), stream: (Stream)File.OpenRead(p)));
        await _s3.UploadObjectsAsync(bucketName, folderPrefix, inputs);
    }

    public Task RenameAsync(string bucketName, string sourceKey, string destinationKey)
    {
        // caller should confirm before invoking
        return _s3.RenameAsync(bucketName, sourceKey, destinationKey);
    }

    public Task DeleteAsync(string bucketName, string keyOrPrefix, bool isFolder = false)
    {
        // caller should confirm before invoking
        return _s3.DeleteAsync(bucketName, keyOrPrefix, isFolder);
    }
}

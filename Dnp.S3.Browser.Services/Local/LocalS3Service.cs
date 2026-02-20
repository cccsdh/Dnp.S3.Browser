using Dnp.S3.Browser.Core.Interfaces;
using Dnp.S3.Browser.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using System.IO.Compression;

namespace Dnp.S3.Browser.Services.Local;

public class LocalS3Service : IS3Service
{
    private readonly string _rootPath;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions = new() { SlidingExpiration = TimeSpan.FromMinutes(5) };

    public LocalS3Service(string rootPath, IMemoryCache cache)
    {
        _rootPath = rootPath;
        _cache = cache;
        Directory.CreateDirectory(_rootPath);
    }

    private string BucketPath(string bucket) => Path.Combine(_rootPath, bucket);

    public Task<IEnumerable<S3BucketInfo>> ListBucketsAsync(CancellationToken cancellationToken = default)
    {
        return _cache.GetOrCreateAsync("buckets_local", entry =>
        {
            entry.SetOptions(_cacheOptions);
            var dirs = Directory.GetDirectories(_rootPath).Select(d => new S3BucketInfo { Name = Path.GetFileName(d), CreationDate = Directory.GetCreationTimeUtc(d) });
            return Task.FromResult<IEnumerable<S3BucketInfo>>(dirs);
        });
    }

    public Task<IEnumerable<S3ObjectInfo>> ListObjectsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        prefix ??= string.Empty;
        var cacheKey = $"objects_local::{bucketName}::{prefix}";
        return _cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.SetOptions(_cacheOptions);
            var basePath = Path.Combine(BucketPath(bucketName), prefix.Replace('/', Path.DirectorySeparatorChar));
            var results = new List<S3ObjectInfo>();
            if (Directory.Exists(basePath))
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    var rel = MakeKey(bucketName, Path.GetRelativePath(BucketPath(bucketName), dir)) + "/";
                    results.Add(new S3ObjectInfo { Key = rel, IsFolder = true });
                }
                foreach (var file in Directory.GetFiles(basePath))
                {
                    var rel = MakeKey(bucketName, Path.GetRelativePath(BucketPath(bucketName), file));
                    var fi = new FileInfo(file);
                    results.Add(new S3ObjectInfo { Key = rel, IsFolder = false, Size = fi.Length, LastModified = fi.LastWriteTimeUtc });
                }
            }
            return Task.FromResult<IEnumerable<S3ObjectInfo>>(results);
        });
    }

    public Task DownloadObjectAsync(string bucketName, string key, Stream destination, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(BucketPath(bucketName), key.Replace('/', Path.DirectorySeparatorChar));
        using var fs = File.OpenRead(path);
        fs.CopyTo(destination);
        destination.Position = 0;
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadFolderAsZipAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        var basePath = Path.Combine(BucketPath(bucketName), prefix.Replace('/', Path.DirectorySeparatorChar));
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (Directory.Exists(basePath))
            {
                foreach (var file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    var entryName = Path.GetRelativePath(basePath, file).Replace('\\', '/');
                    var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fs = File.OpenRead(file);
                    fs.CopyTo(entryStream);
                }
            }
        }
        ms.Position = 0;
        return Task.FromResult<Stream>(ms);
    }

    public Task UploadObjectAsync(string bucketName, string key, Stream source, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(BucketPath(bucketName), key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        using var fs = File.Create(path);
        source.Position = 0;
        source.CopyTo(fs);
        _cache.Remove($"objects_local::{bucketName}::");
        return Task.CompletedTask;
    }

    public Task UploadObjectsAsync(string bucketName, string folderPrefix, IEnumerable<(string fileName, Stream stream)> files, CancellationToken cancellationToken = default)
    {
        foreach (var (fileName, stream) in files)
        {
            var key = string.IsNullOrEmpty(folderPrefix) ? fileName : (folderPrefix.TrimEnd('/') + "/" + fileName);
            UploadObjectAsync(bucketName, key, stream, cancellationToken).GetAwaiter().GetResult();
        }
        return Task.CompletedTask;
    }

    public Task RenameAsync(string bucketName, string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        if (sourceKey.EndsWith('/'))
        {
            var srcPath = Path.Combine(BucketPath(bucketName), sourceKey.Replace('/', Path.DirectorySeparatorChar));
            var destPath = Path.Combine(BucketPath(bucketName), destinationKey.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(destPath);
            foreach (var file in Directory.GetFiles(srcPath, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(srcPath, file);
                var destFile = Path.Combine(destPath, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile) ?? string.Empty);
                File.Move(file, destFile);
            }
            Directory.Delete(srcPath, true);
        }
        else
        {
            var src = Path.Combine(BucketPath(bucketName), sourceKey.Replace('/', Path.DirectorySeparatorChar));
            var dest = Path.Combine(BucketPath(bucketName), destinationKey.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? string.Empty);
            File.Move(src, dest);
        }
        _cache.Remove($"objects_local::{bucketName}::");
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string bucketName, string keyOrPrefix, bool isFolder = false, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(BucketPath(bucketName), keyOrPrefix.Replace('/', Path.DirectorySeparatorChar));
        if (isFolder || keyOrPrefix.EndsWith('/'))
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        else
        {
            if (File.Exists(path)) File.Delete(path);
        }
        _cache.Remove($"objects_local::{bucketName}::");
        return Task.CompletedTask;
    }

    private static string MakeKey(string bucketName, string relPath) => relPath.Replace('\\', '/');
}

using Amazon.S3;
using System.Diagnostics;
using Amazon.S3.Model;
using Dnp.S3.Browser.Core.Interfaces;
using Dnp.S3.Browser.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using System.IO.Compression;

namespace Dnp.S3.Browser.Services.Aws;

public class AwsS3Service : IS3Service, IDisposable
{
    private IAmazonS3? _client;
    private readonly Func<Task<IAmazonS3>> _clientFactory;
    private readonly IMemoryCache _cache;
    private readonly System.Threading.SemaphoreSlim _clientLock = new(1,1);
    private readonly MemoryCacheEntryOptions _cacheOptions = new() { SlidingExpiration = TimeSpan.FromMinutes(5) };

    public AwsS3Service(Func<Task<IAmazonS3>> clientFactory, IMemoryCache cache)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _cache = cache;
    }

    private async Task<IAmazonS3> GetClientAsync()
    {
        Debug.WriteLine($"AwsS3Service.GetClientAsync: client exists={_client != null}");
        if (_client != null) return _client;
        await _clientLock.WaitAsync();
        try
        {
            if (_client != null) return _client;
            Debug.WriteLine("AwsS3Service.GetClientAsync: invoking client factory");
            _client = await _clientFactory();
            Debug.WriteLine($"AwsS3Service.GetClientAsync: client created={_client != null}");
            return _client!;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public async Task<IEnumerable<S3BucketInfo>> ListBucketsAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync("buckets", async entry =>
        {
            entry.SetOptions(_cacheOptions);
            var client = await GetClientAsync();
            var resp = await client.ListBucketsAsync(cancellationToken);
            return resp.Buckets.Select(b => new S3BucketInfo { Name = b.BucketName, CreationDate = b.CreationDate });
        });
    }

    public async Task<IEnumerable<S3ObjectInfo>> ListObjectsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        prefix ??= string.Empty;
        var cacheKey = $"objects::{bucketName}::{prefix}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            Debug.WriteLine($"AwsS3Service.ListObjectsAsync start bucket={bucketName} prefix={prefix}");
            var client = await GetClientAsync().ConfigureAwait(false);
            var request = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix, Delimiter = "/" };
            var results = new List<S3ObjectInfo>();
            ListObjectsV2Response? resp = null;
            try
            {
                do
                {
                    Debug.WriteLine($"AwsS3Service.ListObjectsAsync: requesting ContinuationToken={request.ContinuationToken}");
                    resp = await client.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);
                    Debug.WriteLine($"AwsS3Service.ListObjectsAsync: received {resp.S3Objects.Count} objects {resp.CommonPrefixes.Count} prefixes IsTruncated={resp.IsTruncated}");
                    // folders are in CommonPrefixes
                    foreach (var cp in resp.CommonPrefixes)
                    {
                        results.Add(new S3ObjectInfo { Key = cp, IsFolder = true });
                    }
                    foreach (var o in resp.S3Objects.Where(o => o.Key != prefix))
                    {
                        var isFolder = o.Key.EndsWith('/');
                        results.Add(new S3ObjectInfo { Key = o.Key, IsFolder = isFolder, Size = o.Size, LastModified = o.LastModified });
                    }
                    request.ContinuationToken = resp.NextContinuationToken;
                } while (resp.IsTruncated && !cancellationToken.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AwsS3Service.ListObjectsAsync error: {ex}");
                throw;
            }

            Debug.WriteLine($"AwsS3Service.ListObjectsAsync completed results={results.Count}");
            return results;
        });
    }

    public async Task DownloadObjectAsync(string bucketName, string key, Stream destination, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync();
        var resp = await client.GetObjectAsync(bucketName, key, cancellationToken);
        await resp.ResponseStream.CopyToAsync(destination, cancellationToken);
        destination.Position = 0;
    }

    public async Task<Stream> DownloadFolderAsZipAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var client = await GetClientAsync();
            var listReq = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix };
            ListObjectsV2Response? listResp;
            do
            {
                listResp = await client.ListObjectsV2Async(listReq, cancellationToken);
                foreach (var obj in listResp.S3Objects.Where(o => !o.Key.EndsWith('/')))
                {
                    var getResp = await client.GetObjectAsync(bucketName, obj.Key, cancellationToken);
                    var entryName = obj.Key.Substring(prefix.TrimEnd('/').Length).TrimStart('/');
                    var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await getResp.ResponseStream.CopyToAsync(entryStream, cancellationToken);
                }
                listReq.ContinuationToken = listResp.NextContinuationToken;
            } while (listResp.IsTruncated);
        }
        ms.Position = 0;
        return ms;
    }

    public async Task UploadObjectAsync(string bucketName, string key, Stream source, CancellationToken cancellationToken = default)
    {
        source.Position = 0;
        var client = await GetClientAsync();
        var putReq = new PutObjectRequest { BucketName = bucketName, Key = key, InputStream = source };
        await client.PutObjectAsync(putReq, cancellationToken);
        InvalidateObjectsCache(bucketName, GetPrefixFromKey(key));
    }

    public async Task UploadObjectsAsync(string bucketName, string folderPrefix, IEnumerable<(string fileName, Stream stream)> files, CancellationToken cancellationToken = default)
    {
        foreach (var (fileName, stream) in files)
        {
            var key = string.IsNullOrEmpty(folderPrefix) ? fileName : Path.Combine(folderPrefix.TrimEnd('/'), fileName).Replace('\\', '/');
            await UploadObjectAsync(bucketName, key, stream, cancellationToken);
        }
    }

    public async Task RenameAsync(string bucketName, string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        // If sourceKey is a prefix (folder), copy objects under prefix
        if (sourceKey.EndsWith('/'))
        {
            var client = await GetClientAsync();
            var listReq = new ListObjectsV2Request { BucketName = bucketName, Prefix = sourceKey };
            ListObjectsV2Response? listResp;
            do
            {
                listResp = await client.ListObjectsV2Async(listReq, cancellationToken);
                foreach (var obj in listResp.S3Objects)
                {
                    var relative = obj.Key.Substring(sourceKey.Length);
                    var dest = destinationKey.TrimEnd('/') + "/" + relative;
                    await CopyObjectAsync(bucketName, obj.Key, dest, cancellationToken);
                    await client.DeleteObjectAsync(bucketName, obj.Key, cancellationToken);
                }
                listReq.ContinuationToken = listResp.NextContinuationToken;
            } while (listResp.IsTruncated);
        }
        else
        {
            await CopyObjectAsync(bucketName, sourceKey, destinationKey, cancellationToken);
            var client = await GetClientAsync();
            await client.DeleteObjectAsync(bucketName, sourceKey, cancellationToken);
        }
        InvalidateObjectsCache(bucketName, GetPrefixFromKey(sourceKey));
        InvalidateObjectsCache(bucketName, GetPrefixFromKey(destinationKey));
    }

    private async Task CopyObjectAsync(string bucketName, string sourceKey, string destinationKey, CancellationToken cancellationToken)
    {
        var client = await GetClientAsync();
        var copyReq = new CopyObjectRequest { SourceBucket = bucketName, SourceKey = sourceKey, DestinationBucket = bucketName, DestinationKey = destinationKey };
        await client.CopyObjectAsync(copyReq, cancellationToken);
    }

    public async Task DeleteAsync(string bucketName, string keyOrPrefix, bool isFolder = false, CancellationToken cancellationToken = default)
    {
        if (isFolder || keyOrPrefix.EndsWith('/'))
        {
            var client = await GetClientAsync();
            var listReq = new ListObjectsV2Request { BucketName = bucketName, Prefix = keyOrPrefix };
            ListObjectsV2Response? listResp;
            var toDelete = new List<KeyVersion>();
            do
            {
                listResp = await client.ListObjectsV2Async(listReq, cancellationToken);
                toDelete.AddRange(listResp.S3Objects.Select(o => new KeyVersion { Key = o.Key }));
                listReq.ContinuationToken = listResp.NextContinuationToken;
            } while (listResp.IsTruncated);

            if (toDelete.Any())
            {
                var deleteReq = new DeleteObjectsRequest { BucketName = bucketName, Objects = toDelete };
                await client.DeleteObjectsAsync(deleteReq, cancellationToken);
            }
        }
        else
        {
            var client = await GetClientAsync();
            await client.DeleteObjectAsync(bucketName, keyOrPrefix, cancellationToken);
        }
        InvalidateObjectsCache(bucketName, GetPrefixFromKey(keyOrPrefix));
    }

    private void InvalidateObjectsCache(string bucketName, string? prefix)
    {
        // Simple: remove any cached entry that starts with this bucketName
        // MemoryCache doesn't provide enumeration by default; in real app use a cache wrapper. Here we'll remove a few predictable keys.
        _cache.Remove($"objects::{bucketName}::{prefix}");
        _cache.Remove("buckets");
    }

    private static string? GetPrefixFromKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var idx = key.LastIndexOf('/');
        return idx >= 0 ? key.Substring(0, idx + 1) : string.Empty;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

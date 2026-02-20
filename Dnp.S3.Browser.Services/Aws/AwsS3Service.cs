using Amazon.S3;
using Amazon.S3.Model;
using Dnp.S3.Browser.Core.Interfaces;
using Dnp.S3.Browser.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using System.IO.Compression;

namespace Dnp.S3.Browser.Services.Aws;

public class AwsS3Service : IS3Service, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions = new() { SlidingExpiration = TimeSpan.FromMinutes(5) };

    public AwsS3Service(IAmazonS3 client, IMemoryCache cache)
    {
        _client = client;
        _cache = cache;
    }

    public async Task<IEnumerable<S3BucketInfo>> ListBucketsAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync("buckets", async entry =>
        {
            entry.SetOptions(_cacheOptions);
            var resp = await _client.ListBucketsAsync(cancellationToken);
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
            var request = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix, Delimiter = "/" };
            var results = new List<S3ObjectInfo>();
            ListObjectsV2Response? resp;
            do
            {
                resp = await _client.ListObjectsV2Async(request, cancellationToken);
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
            } while (resp.IsTruncated);

            return results;
        });
    }

    public async Task DownloadObjectAsync(string bucketName, string key, Stream destination, CancellationToken cancellationToken = default)
    {
        var resp = await _client.GetObjectAsync(bucketName, key, cancellationToken);
        await resp.ResponseStream.CopyToAsync(destination, cancellationToken);
        destination.Position = 0;
    }

    public async Task<Stream> DownloadFolderAsZipAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var listReq = new ListObjectsV2Request { BucketName = bucketName, Prefix = prefix };
            ListObjectsV2Response? listResp;
            do
            {
                listResp = await _client.ListObjectsV2Async(listReq, cancellationToken);
                foreach (var obj in listResp.S3Objects.Where(o => !o.Key.EndsWith('/')))
                {
                    var getResp = await _client.GetObjectAsync(bucketName, obj.Key, cancellationToken);
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
        var putReq = new PutObjectRequest { BucketName = bucketName, Key = key, InputStream = source };
        await _client.PutObjectAsync(putReq, cancellationToken);
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
            var listReq = new ListObjectsV2Request { BucketName = bucketName, Prefix = sourceKey };
            ListObjectsV2Response? listResp;
            do
            {
                listResp = await _client.ListObjectsV2Async(listReq, cancellationToken);
                foreach (var obj in listResp.S3Objects)
                {
                    var relative = obj.Key.Substring(sourceKey.Length);
                    var dest = destinationKey.TrimEnd('/') + "/" + relative;
                    await CopyObjectAsync(bucketName, obj.Key, dest, cancellationToken);
                    await _client.DeleteObjectAsync(bucketName, obj.Key, cancellationToken);
                }
                listReq.ContinuationToken = listResp.NextContinuationToken;
            } while (listResp.IsTruncated);
        }
        else
        {
            await CopyObjectAsync(bucketName, sourceKey, destinationKey, cancellationToken);
            await _client.DeleteObjectAsync(bucketName, sourceKey, cancellationToken);
        }
        InvalidateObjectsCache(bucketName, GetPrefixFromKey(sourceKey));
        InvalidateObjectsCache(bucketName, GetPrefixFromKey(destinationKey));
    }

    private Task CopyObjectAsync(string bucketName, string sourceKey, string destinationKey, CancellationToken cancellationToken)
    {
        var copyReq = new CopyObjectRequest { SourceBucket = bucketName, SourceKey = sourceKey, DestinationBucket = bucketName, DestinationKey = destinationKey };
        return _client.CopyObjectAsync(copyReq, cancellationToken);
    }

    public async Task DeleteAsync(string bucketName, string keyOrPrefix, bool isFolder = false, CancellationToken cancellationToken = default)
    {
        if (isFolder || keyOrPrefix.EndsWith('/'))
        {
            var listReq = new ListObjectsV2Request { BucketName = bucketName, Prefix = keyOrPrefix };
            ListObjectsV2Response? listResp;
            var toDelete = new List<KeyVersion>();
            do
            {
                listResp = await _client.ListObjectsV2Async(listReq, cancellationToken);
                toDelete.AddRange(listResp.S3Objects.Select(o => new KeyVersion { Key = o.Key }));
                listReq.ContinuationToken = listResp.NextContinuationToken;
            } while (listResp.IsTruncated);

            if (toDelete.Any())
            {
                var deleteReq = new DeleteObjectsRequest { BucketName = bucketName, Objects = toDelete };
                await _client.DeleteObjectsAsync(deleteReq, cancellationToken);
            }
        }
        else
        {
            await _client.DeleteObjectAsync(bucketName, keyOrPrefix, cancellationToken);
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

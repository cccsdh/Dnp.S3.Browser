using Dnp.S3.Browser.Core.Models;

namespace Dnp.S3.Browser.Core.Interfaces;

public interface IS3Service
{
    // List all buckets the configured credentials can access
    Task<IEnumerable<S3BucketInfo>> ListBucketsAsync(CancellationToken cancellationToken = default);

    // List objects in a bucket at a prefix (folder). Use empty string or null for root.
    Task<IEnumerable<S3ObjectInfo>> ListObjectsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default);

    // Download a single object to a stream
    Task DownloadObjectAsync(string bucketName, string key, Stream destination, CancellationToken cancellationToken = default);

    // Download a folder (prefix) as a zip stream. Implementations can stream directly.
    Task<Stream> DownloadFolderAsZipAsync(string bucketName, string prefix, CancellationToken cancellationToken = default);

    // Upload a single object from stream. If folderPrefix provided, key is relative to it
    Task UploadObjectAsync(string bucketName, string key, Stream source, CancellationToken cancellationToken = default);

    // Upload multiple objects
    Task UploadObjectsAsync(string bucketName, string folderPrefix, IEnumerable<(string fileName, Stream stream)> files, CancellationToken cancellationToken = default);

    // Rename an object or folder (folder rename may be implemented as copy+delete)
    Task RenameAsync(string bucketName, string sourceKey, string destinationKey, CancellationToken cancellationToken = default);

    // Delete object or folder (if prefix indicates folder, delete all objects under prefix)
    Task DeleteAsync(string bucketName, string keyOrPrefix, bool isFolder = false, CancellationToken cancellationToken = default);
}

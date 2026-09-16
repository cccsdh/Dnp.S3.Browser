namespace Dnp.S3.Browser.Core.Models;

public record S3ObjectInfo
{
    public string Key { get; init; } = string.Empty;
    public bool IsFolder { get; init; }
    public long? Size { get; init; }
    public DateTime? LastModified { get; init; }

    // Display name: the last path segment of Key, independent of the current breadcrumb prefix.
    public string Name
    {
        get
        {
            var trimmed = Key.TrimEnd('/');
            var slashIndex = trimmed.LastIndexOf('/');
            return slashIndex >= 0 ? trimmed[(slashIndex + 1)..] : trimmed;
        }
    }
}

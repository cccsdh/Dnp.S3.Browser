namespace Dnp.S3.Browser.Core.Models;

public record S3ObjectInfo
{
    public string Key { get; init; } = string.Empty;
    public bool IsFolder { get; init; }
    public long? Size { get; init; }
    public DateTime? LastModified { get; init; }
}

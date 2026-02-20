namespace Dnp.S3.Browser.Core.Models;

public record S3BucketInfo
{
    public string Name { get; init; } = string.Empty;
    public DateTime? CreationDate { get; init; }
}

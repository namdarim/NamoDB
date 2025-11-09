namespace Namo.Domain.DBSync;

public sealed class DbSyncManifest
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public DateTimeOffset LastModifiedUtc { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; }

    public bool IsEmpty() => string.IsNullOrWhiteSpace(VersionId);
}

namespace Namo.Domain.DBSync;

public sealed class DbSyncManifest
{
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;

    // Remote head
    public string VersionId { get; set; } = string.Empty; // remote version id
    public string ETag { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;    // by policy = VersionId (remote)

    public long ContentLength { get; set; }
    public DateTimeOffset LastModifiedUtc { get; set; }

    // Apply info
    public DateTimeOffset AppliedAtUtc { get; set; }

    // NEW: local content hash at the time of last apply/push
    public string LocalContentSha256 { get; set; } = string.Empty;

    public bool IsEmpty() => string.IsNullOrEmpty(VersionId);
}

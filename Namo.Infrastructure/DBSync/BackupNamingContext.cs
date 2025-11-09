namespace Namo.Infrastructure.DBSync;

/// <summary>
/// Context for constructing a deterministic, self-explanatory backup file name.
/// </summary>
public sealed class BackupNamingContext
{
    public required string LocalDbPath { get; init; }
    public required string LocalContentSha256 { get; init; } // current local file hash (may be "")
    public required string LocalVersionId { get; init; }     // manifest.VersionId ("" if none)
    public required string RemoteVersionId { get; init; }    // latest remote tip
    public required DateTimeOffset AppliedAtUtc { get; init; }
    public required DateTimeOffset NowUtc { get; init; }
    public required string Reason { get; init; }             // e.g., "pull-overwrite"
}

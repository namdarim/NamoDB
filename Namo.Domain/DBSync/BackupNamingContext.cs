namespace Namo.Domain.DBSync;

/// <summary>
/// Context for constructing a deterministic, self-explanatory backup file name.
/// </summary>
public sealed class BackupNamingContext
{
    public required string RemoteVersionId { get; init; }    // latest remote tip
    public required string Reason { get; init; }             // e.g., "pull-overwrite"
}

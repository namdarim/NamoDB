namespace Namo.Domain.DBSync;

/// <summary>
/// Context for constructing a deterministic, self-explanatory backup file name.
/// </summary>
public sealed record BackupNamingContext(
    string? RemoteVersionId, // latest remote tip
    string Reason// e.g., "pull-overwrite"
    );

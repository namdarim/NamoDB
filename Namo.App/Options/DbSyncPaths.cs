namespace Namo.App.Options;

public sealed class DbSyncPaths
{
    public required string LocalDbPath { get; init; }
    public required string LocalDbBackupsDir { get; init; }
    public required string SnapshotDir { get; init; }
    public string? ManifestPath { get; init; } // used by WIN file-based KV
}

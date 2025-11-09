namespace Namo.App.Options;

public sealed class DbSyncPaths
{
    public string LocalDbPath { get; set; } = "";
    public string SnapshotDir { get; set; } = "";
    public string? ManifestPath { get; set; } // used by WIN file-based KV
}

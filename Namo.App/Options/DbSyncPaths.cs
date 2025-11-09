namespace Namo.App.DBSync;

public sealed class DbSyncPaths
{
    public string LocalDbPath { get; set; } = "";
    public string SnapshotDir { get; set; } = "";
    public string? ManifestPath { get; set; } // used by WIN file-based KV
}

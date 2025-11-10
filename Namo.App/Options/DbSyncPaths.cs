namespace Namo.App.Options;

public sealed record DbSyncPaths(string LocalDbPath, string LocalDbBackupsDir, string SnapshotDir, string? ManifestPath/* used by WIN file-based KV*/);

using Microsoft.Data.Sqlite;

namespace Namo.Infrastructure.DBSync;

// Creates a consistent snapshot of a live SQLite database using the native backup API.
public static class SqliteBackup
{
    // Returns the absolute path of the created snapshot file.
    public static string CreateSnapshot(string sourceDbPath, string snapshotsDir, string? snapshotFileName = null, bool verifyIntegrity = false)
    {
        if (string.IsNullOrWhiteSpace(sourceDbPath)) throw new ArgumentNullException(nameof(sourceDbPath));
        if (string.IsNullOrWhiteSpace(snapshotsDir)) throw new ArgumentNullException(nameof(snapshotsDir));

        Directory.CreateDirectory(snapshotsDir);

        var name = snapshotFileName ?? $"{Path.GetFileNameWithoutExtension(sourceDbPath)}.snapshot-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.db";
        var destPath = Path.Combine(snapshotsDir, name);
        if (File.Exists(destPath)) File.Delete(destPath);

        var srcCsb = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDbPath,
            Mode = SqliteOpenMode.ReadOnly,   // ensures we do not write to the live DB
            Cache = SqliteCacheMode.Shared
        };

        var dstCsb = new SqliteConnectionStringBuilder
        {
            DataSource = destPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };

        using (var src = new SqliteConnection(srcCsb.ConnectionString))
        using (var dst = new SqliteConnection(dstCsb.ConnectionString))
        {
            src.Open();
            dst.Open();

            // Take a consistent snapshot at the time of calling.
            src.BackupDatabase(dst);

            if (verifyIntegrity)
            {
                using var cmd = dst.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var result = (string)cmd.ExecuteScalar()!;
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"SQLite integrity_check failed with result: {result}");
            }
        }

        return destPath;
    }
}

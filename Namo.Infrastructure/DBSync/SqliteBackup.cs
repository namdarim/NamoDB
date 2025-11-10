using Microsoft.Data.Sqlite;

namespace Namo.Infrastructure.DBSync;

// Creates a consistent snapshot of a live SQLite database using the native backup API.
using System;
using System.IO;
using Microsoft.Data.Sqlite;

public static class SqliteBackup
{
    // Returns the absolute path of the created snapshot file.
    public static string CreateSnapshot(
        string sourceDbPath,
        string snapshotsDir,
        string? snapshotFileName = null,
        bool verifyIntegrity = false)
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
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = false // avoid holding file handles via pooling
        };

        var dstCsb = new SqliteConnectionStringBuilder
        {
            DataSource = destPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false // critical to release file handles deterministically
        };

        try
        {
            using (var src = new SqliteConnection(srcCsb.ConnectionString))
            using (var dst = new SqliteConnection(dstCsb.ConnectionString))
            {
                src.Open();
                dst.Open();

                // Ensure the snapshot is a single-file DB (no WAL alongside), optional but useful.
                using (var pragma = dst.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA journal_mode=DELETE;";
                    pragma.ExecuteNonQuery();
                }

                // Take a consistent snapshot at the time of calling.
                src.BackupDatabase(dst);
            }

            if (verifyIntegrity)
            {
                var verifyCsb = new SqliteConnectionStringBuilder
                {
                    DataSource = destPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false // also disable pooling for the check connection
                };

                using var chk = new SqliteConnection(verifyCsb.ConnectionString);
                chk.Open();
                using var cmd = chk.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var result = (string)cmd.ExecuteScalar()!;
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"SQLite integrity_check failed with result: {result}");
            }

            return destPath;
        }
        finally
        {
            // Ensure no pooled connections keep the file in use.
            SqliteConnection.ClearAllPools();
        }
    }
}

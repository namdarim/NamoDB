using Microsoft.Data.Sqlite;
using Namo.Domain;

namespace Namo.Infrastructure;

public sealed class SqliteSnapshotFactory : ISqliteSnapshotFactory
{
    public async Task<string> CreateSnapshotAsync(string liveDatabasePath, string snapshotDestinationPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(liveDatabasePath);
        ArgumentException.ThrowIfNullOrEmpty(snapshotDestinationPath);

        if (!File.Exists(liveDatabasePath))
        {
            throw new FileNotFoundException("Live database not found.", liveDatabasePath);
        }

        var destinationDirectory = Path.GetDirectoryName(snapshotDestinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var tmpPath = snapshotDestinationPath + ".tmp";
        if (File.Exists(tmpPath))
        {
            File.Delete(tmpPath);
        }

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = liveDatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.DefaultTimeout = 30;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var checkpoint = connection.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var vacuum = connection.CreateCommand())
        {
            vacuum.CommandText = $"VACUUM INTO '{EscapeSqliteLiteral(tmpPath)}'";
            await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(snapshotDestinationPath))
        {
            File.Delete(snapshotDestinationPath);
        }

        File.Move(tmpPath, snapshotDestinationPath);
        return snapshotDestinationPath;
    }

    private static string EscapeSqliteLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}

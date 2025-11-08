using Microsoft.Data.Sqlite;
using Namo.Domain;
using SQLitePCL;

namespace Namo.Infrastructure;

public sealed class SqliteLocalDatabaseApplier : ILocalDatabaseApplier
{
    public async Task ApplySnapshotAsync(string liveDatabasePath, string snapshotPath, SqliteApplyOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(liveDatabasePath);
        ArgumentException.ThrowIfNullOrEmpty(snapshotPath);
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(snapshotPath))
        {
            throw new FileNotFoundException("Snapshot file not found.", snapshotPath);
        }

        var attempts = options.BusyRetryCount < 0 ? 0 : options.BusyRetryCount;
        for (var attempt = 0; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ApplyInternalAsync(liveDatabasePath, snapshotPath, options, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (SqliteException ex) when (IsBusy(ex) && attempt < attempts)
            {
                await Task.Delay(options.BusyTimeout, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task ApplyInternalAsync(string liveDatabasePath, string snapshotPath, SqliteApplyOptions options, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(liveDatabasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (options.CreatePreviousBackup && File.Exists(liveDatabasePath))
        {
            var backupPath = liveDatabasePath + ".prev";
            File.Copy(liveDatabasePath, backupPath, overwrite: true);
        }

        await using var destination = CreateConnection(liveDatabasePath, writable: true, createIfMissing: true, options.BusyTimeout);
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (options.CheckpointWal)
        {
            await using var checkpoint = destination.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var source = CreateConnection(snapshotPath, writable: false, createIfMissing: false, TimeSpan.FromSeconds(30));
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);

        source.BackupDatabase(destination);

        await using (var integrity = destination.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("SQLite integrity_check failed.");
            }
        }
    }

    private static SqliteConnection CreateConnection(string path, bool writable, bool createIfMissing, TimeSpan busyTimeout)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Cache = SqliteCacheMode.Shared,
            Mode = writable ? (createIfMissing ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite) : SqliteOpenMode.ReadOnly
        };

        var connection = new SqliteConnection(builder.ToString())
        {
            DefaultTimeout = Math.Max(1, (int)Math.Ceiling(busyTimeout.TotalSeconds))
        };

        return connection;
    }

    private static bool IsBusy(SqliteException exception)
        => exception.SqliteErrorCode is (int)raw.SQLITE_BUSY or (int)raw.SQLITE_LOCKED;
}

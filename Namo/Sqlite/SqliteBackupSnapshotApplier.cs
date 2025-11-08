using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Namo.Sqlite;

public sealed class SqliteBackupSnapshotApplier : ISqliteSnapshotApplier
{
    private readonly string _databasePath;
    private readonly string? _previousBackupPath;
    private readonly bool _checkpointWalBeforeRestore;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public SqliteBackupSnapshotApplier(string databasePath, string? previousBackupPath = null, bool checkpointWalBeforeRestore = true)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(databasePath));
        }

        _databasePath = databasePath;
        _previousBackupPath = previousBackupPath;
        _checkpointWalBeforeRestore = checkpointWalBeforeRestore;
    }

    public async Task ApplySnapshotAsync(string snapshotFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshotFilePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(snapshotFilePath));
        }

        if (!File.Exists(snapshotFilePath))
        {
            throw new FileNotFoundException("Snapshot file not found.", snapshotFilePath);
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!string.IsNullOrWhiteSpace(_previousBackupPath) && File.Exists(_databasePath))
            {
                var previousDirectory = Path.GetDirectoryName(_previousBackupPath);
                if (!string.IsNullOrEmpty(previousDirectory))
                {
                    Directory.CreateDirectory(previousDirectory);
                }

                File.Copy(_databasePath, _previousBackupPath!, true);
            }

            var destinationBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default
            };

            await using var destination = new SqliteConnection(destinationBuilder.ToString());
            await destination.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (_checkpointWalBeforeRestore)
            {
                await using var checkpointCommand = destination.CreateCommand();
                checkpointCommand.CommandText = "PRAGMA wal_checkpoint(FULL);";
                await checkpointCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var sourceBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = snapshotFilePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private
            };

            await using (var source = new SqliteConnection(sourceBuilder.ToString()))
            {
                await source.OpenAsync(cancellationToken).ConfigureAwait(false);
                source.BackupDatabase(destination);
            }

            await using (var integrity = destination.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = (string?)await integrity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Integrity check failed with result '{result ?? "unknown"}'.");
                }
            }
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(_previousBackupPath) && File.Exists(_previousBackupPath!) && File.Exists(_databasePath))
            {
                try
                {
                    File.Copy(_previousBackupPath!, _databasePath, true);
                }
                catch
                {
                    // Best effort rollback.
                }
            }

            throw;
        }
        finally
        {
            _mutex.Release();
        }
    }
}

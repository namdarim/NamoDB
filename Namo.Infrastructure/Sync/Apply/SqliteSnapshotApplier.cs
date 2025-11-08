using Microsoft.Data.Sqlite;
using Namo.Domain.Contracts.Sync;

namespace Namo.Infrastructure.Sync.Apply;

public sealed class SqliteSnapshotApplier : ISqliteSnapshotApplier
{
    private const int BusyTimeoutSeconds = 30;

    public async Task ApplySnapshotAsync(string liveDatabasePath, string snapshotPath, SqliteSnapshotApplierOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(liveDatabasePath);
        ArgumentException.ThrowIfNullOrEmpty(snapshotPath);
        options ??= new SqliteSnapshotApplierOptions(false);

        if (!File.Exists(snapshotPath))
        {
            throw new FileNotFoundException("Snapshot file not found.", snapshotPath);
        }

        if (!File.Exists(liveDatabasePath))
        {
            throw new FileNotFoundException("Live database not found.", liveDatabasePath);
        }

        var rollbackCopyPath = options.RollbackCopyPath ?? (liveDatabasePath + ".prev");

        await using var destinationConnection = new SqliteConnection($"Data Source={liveDatabasePath};Mode=ReadWrite;Cache=Shared;");
        destinationConnection.DefaultTimeout = BusyTimeoutSeconds;
        await destinationConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = destinationConnection.CreateCommand())
        {
            command.CommandText = "PRAGMA wal_checkpoint(FULL);";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (options.CreateRollbackCopy)
        {
            await CopyFileAsync(liveDatabasePath, rollbackCopyPath, cancellationToken).ConfigureAwait(false);
        }

        await using var sourceConnection = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadOnly;Cache=Shared;");
        sourceConnection.DefaultTimeout = BusyTimeoutSeconds;
        await sourceConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        sourceConnection.BackupDatabase(destinationConnection);

        await using (var command = destinationConnection.CreateCommand())
        {
            command.CommandText = "PRAGMA integrity_check;";
            var result = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Integrity check failed after applying snapshot.");
            }
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

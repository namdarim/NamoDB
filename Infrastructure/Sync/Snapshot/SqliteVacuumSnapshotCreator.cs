using Microsoft.Data.Sqlite;
using Namo.Domain.Contracts.Sync;

namespace Namo.Infrastructure.Sync.Snapshot;

public sealed class SqliteVacuumSnapshotCreator : ISqliteSnapshotCreator
{
    private const int BusyTimeoutSeconds = 30;

    public async Task<SnapshotCreationResult> CreateSnapshotAsync(string liveDatabasePath, string snapshotOutputPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(liveDatabasePath);
        ArgumentException.ThrowIfNullOrEmpty(snapshotOutputPath);

        var snapshotDirectory = Path.GetDirectoryName(snapshotOutputPath);
        if (!string.IsNullOrEmpty(snapshotDirectory))
        {
            Directory.CreateDirectory(snapshotDirectory);
        }

        if (File.Exists(snapshotOutputPath))
        {
            File.Delete(snapshotOutputPath);
        }

        await using var connection = new SqliteConnection($"Data Source={liveDatabasePath};Mode=ReadOnly;Cache=Shared;");
        connection.DefaultTimeout = BusyTimeoutSeconds;
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var escapedPath = snapshotOutputPath.Replace("'", "''");
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"VACUUM INTO '{escapedPath}'";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var fileInfo = new FileInfo(snapshotOutputPath);
        if (!fileInfo.Exists)
        {
            throw new InvalidOperationException("Snapshot creation failed; output file missing.");
        }

        var sha256Hex = await ComputeSha256Async(snapshotOutputPath, cancellationToken).ConfigureAwait(false);
        return new SnapshotCreationResult(snapshotOutputPath, fileInfo.Length, sha256Hex);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        using var hasher = System.Security.Cryptography.SHA256.Create();
        var hash = await hasher.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}

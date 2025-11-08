using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Namo.Sqlite;

public sealed class SqliteVacuumSnapshotProvider : ISqliteSnapshotProvider
{
    private readonly string _databasePath;

    public SqliteVacuumSnapshotProvider(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(databasePath));
        }

        _databasePath = databasePath;
    }

    public async Task CreateSnapshotAsync(string snapshotFilePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(snapshotFilePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(snapshotFilePath));
        }

        var directory = Path.GetDirectoryName(snapshotFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Default
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var escapedPath = snapshotFilePath.Replace("\"", "\"\"", StringComparison.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = $"VACUUM INTO \"{escapedPath}\"";
        command.CommandTimeout = 0;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

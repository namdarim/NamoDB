namespace Namo.Domain;

public sealed record SqliteApplyOptions(bool CheckpointWal, bool CreatePreviousBackup, TimeSpan BusyTimeout, int BusyRetryCount);

public interface ILocalDatabaseApplier
{
    Task ApplySnapshotAsync(string liveDatabasePath, string snapshotPath, SqliteApplyOptions options, CancellationToken cancellationToken);
}

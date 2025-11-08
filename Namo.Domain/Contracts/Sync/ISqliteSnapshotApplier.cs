namespace Namo.Domain.Contracts.Sync;

public interface ISqliteSnapshotApplier
{
    Task ApplySnapshotAsync(string liveDatabasePath, string snapshotPath, SqliteSnapshotApplierOptions options, CancellationToken cancellationToken);
}

namespace Namo.Domain.Contracts.Sync;

public interface ISqliteSnapshotCreator
{
    Task<SnapshotCreationResult> CreateSnapshotAsync(string liveDatabasePath, string snapshotOutputPath, CancellationToken cancellationToken);
}

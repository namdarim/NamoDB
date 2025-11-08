namespace Namo.Domain;

public interface ISqliteSnapshotFactory
{
    Task<string> CreateSnapshotAsync(string liveDatabasePath, string snapshotDestinationPath, CancellationToken cancellationToken);
}

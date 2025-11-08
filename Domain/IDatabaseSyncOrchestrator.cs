namespace Namo.Domain;

public interface IDatabaseSyncOrchestrator
{
    Task<PublishResult> PublishAsync(string liveDatabasePath, string bucketName, string objectKey, CancellationToken cancellationToken);
    Task<SyncResult> SyncAsync(string bucketName, string objectKey, string liveDatabasePath, string snapshotWorkingPath, CancellationToken cancellationToken);
}

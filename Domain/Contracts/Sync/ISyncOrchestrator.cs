using Namo.Domain.Contracts.Cloud;

namespace Namo.Domain.Contracts.Sync;

public interface ISyncOrchestrator
{
    Task<VersionedUploadResult> PublishAsync(string liveDatabasePath, CloudObjectIdentifier identifier, CancellationToken cancellationToken);

    Task<SyncResult> SyncAsync(string liveDatabasePath, string snapshotTempPath, CloudObjectIdentifier identifier, CancellationToken cancellationToken);
}

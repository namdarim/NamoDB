using Namo.Domain.Contracts.Cloud;

namespace Namo.Domain.Contracts.Sync;

public interface ISnapshotDownloader
{
    Task<DownloadedSnapshot> DownloadAsync(CloudObjectIdentifier identifier, VersionedObjectMetadata metadata, string targetPath, CancellationToken cancellationToken);
}

public sealed record DownloadedSnapshot(string SnapshotPath, VersionedObjectMetadata Metadata, string Sha256Hex, long SizeBytes);

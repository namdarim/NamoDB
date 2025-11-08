namespace Namo.Domain;

public interface IVersionedObjectClient
{
    Task<VersionedObjectMetadata?> GetLatestAsync(string bucketName, string objectKey, CancellationToken cancellationToken);
    Task DownloadVersionAsync(string bucketName, string objectKey, string versionId, Stream destination, CancellationToken cancellationToken);
    Task<VersionedObjectMetadata> UploadSnapshotAsync(string bucketName, string objectKey, Stream content, string sha256Hex, DateTimeOffset createdAtUtc, IReadOnlyDictionary<string, string>? additionalMetadata, CancellationToken cancellationToken);
}

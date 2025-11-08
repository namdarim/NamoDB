namespace Namo.Domain.Contracts.Cloud;

public interface IVersionedObjectClient
{
    Task<VersionedObjectMetadata?> GetLatestVersionAsync(CloudObjectIdentifier identifier, CancellationToken cancellationToken);

    Task<VersionedUploadResult> UploadSnapshotAsync(
        CloudObjectIdentifier identifier,
        Stream content,
        string sha256Hex,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);

    Task<VersionedDownloadResult> DownloadVersionAsync(
        CloudObjectIdentifier identifier,
        string versionId,
        CancellationToken cancellationToken);
}

public sealed class VersionedDownloadResult : IAsyncDisposable, IDisposable
{
    public VersionedDownloadResult(VersionedObjectMetadata metadata, Stream contentStream)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        ContentStream = contentStream ?? throw new ArgumentNullException(nameof(contentStream));
    }

    public VersionedObjectMetadata Metadata { get; }

    public Stream ContentStream { get; }

    public ValueTask DisposeAsync()
    {
        return ContentStream.DisposeAsync();
    }

    public void Dispose()
    {
        ContentStream.Dispose();
    }
}

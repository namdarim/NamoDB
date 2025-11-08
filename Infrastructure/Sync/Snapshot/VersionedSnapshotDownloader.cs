using System.Security.Cryptography;
using Namo.Domain.Contracts.Cloud;
using Namo.Domain.Contracts.Sync;

namespace Namo.Infrastructure.Sync.Snapshot;

public sealed class VersionedSnapshotDownloader : ISnapshotDownloader
{
    private const int BufferSize = 81920;
    private readonly IVersionedObjectClient _client;

    public VersionedSnapshotDownloader(IVersionedObjectClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<DownloadedSnapshot> DownloadAsync(CloudObjectIdentifier identifier, VersionedObjectMetadata metadata, string targetPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrEmpty(targetPath);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = targetPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            await using var download = await _client.DownloadVersionAsync(identifier, metadata.VersionId, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(download.Metadata.VersionId, metadata.VersionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Downloaded version identifier mismatch.");
            }

            var effectiveMetadata = download.Metadata;
            await using var responseStream = download.ContentStream;
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[BufferSize];
            int bytesRead;
            long totalBytes = 0;
            while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                hasher.AppendData(buffer, 0, bytesRead);
                totalBytes += bytesRead;
            }
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var hash = hasher.GetHashAndReset();
            var sha256Hex = Convert.ToHexString(hash);
            if (!sha256Hex.Equals(effectiveMetadata.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Downloaded snapshot failed SHA-256 verification.");
            }

            if (totalBytes != effectiveMetadata.ContentLength)
            {
                throw new InvalidOperationException("Downloaded snapshot size mismatch.");
            }

            File.Move(tempPath, targetPath, overwrite: true);
            return new DownloadedSnapshot(targetPath, effectiveMetadata, sha256Hex.ToUpperInvariant(), totalBytes);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }
}

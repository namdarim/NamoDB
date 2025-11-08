using System.IO;
using System.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using Namo.Domain.Contracts.Cloud;

namespace Namo.Infrastructure.Cloud.S3;

public sealed class S3VersionedObjectClient : IVersionedObjectClient
{
    private readonly IAmazonS3 _s3;

    public S3VersionedObjectClient(IAmazonS3 s3)
    {
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
    }

    /// <summary>
    /// Retrieves metadata for the newest downloadable version of the specified object. AWS returns versions newest-first,
    /// so we iterate pages until the first non-delete entry for the key, and callers must download using the returned VersionId to remain TOCTOU-safe.
    /// </summary>
    public async Task<VersionedObjectMetadata?> GetLatestVersionAsync(CloudObjectIdentifier identifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        var request = new ListVersionsRequest
        {
            BucketName = identifier.BucketName,
            Prefix = identifier.ObjectKey
        };

        S3ObjectVersion? version = null;
        ListVersionsResponse response;
        // AWS ListObjectVersions returns versions newest-first, so the first non-delete match is the latest downloadable version.
        do
        {
            response = await _s3.ListVersionsAsync(request, cancellationToken).ConfigureAwait(false);

            foreach (var candidate in response.Versions)
            {
                if (!string.Equals(candidate.Key, identifier.ObjectKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (candidate.IsDeleteMarker)
                {
                    continue;
                }

                version = candidate;
                break;
            }

            if (version is not null)
            {
                break;
            }

            // Continue paging until we find the newest non-delete version for the target key.
            request.KeyMarker = response.NextKeyMarker;
            request.VersionIdMarker = response.NextVersionIdMarker;
        }
        while (response.IsTruncated);

        if (version is null)
        {
            return null;
        }

        return new VersionedObjectMetadata(
            version.VersionId,
            version.ETag?.Trim('"') ?? string.Empty,
            await ResolveSha256Async(identifier, version.VersionId, cancellationToken).ConfigureAwait(false),
            version.LastModified.ToUniversalTime(),
            version.Size);
    }

    public async Task<VersionedUploadResult> UploadSnapshotAsync(CloudObjectIdentifier identifier, Stream content, string sha256Hex, DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(sha256Hex);

        var request = new PutObjectRequest
        {
            BucketName = identifier.BucketName,
            Key = identifier.ObjectKey,
            InputStream = content,
            AutoCloseStream = false
        };
        request.Metadata.Add("sha256", sha256Hex);
        request.Metadata.Add("created-at-utc", createdAtUtc.UtcDateTime.ToString("O"));

        var response = await _s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(response.VersionId))
        {
            throw new InvalidOperationException("VersionId was not returned by S3 for the uploaded snapshot.");
        }

        var metadata = await GetObjectMetadataAsync(identifier, response.VersionId, cancellationToken).ConfigureAwait(false);

        return new VersionedUploadResult(
            identifier,
            metadata,
            createdAtUtc,
            sha256Hex);
    }

    public async Task<VersionedDownloadResult> DownloadVersionAsync(CloudObjectIdentifier identifier, string versionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentException.ThrowIfNullOrEmpty(versionId);

        var request = new GetObjectRequest
        {
            BucketName = identifier.BucketName,
            Key = identifier.ObjectKey,
            VersionId = versionId
        };

        var response = await _s3.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        var sha256 = ResolveSha256(response.Metadata);
        if (string.IsNullOrEmpty(sha256))
        {
            response.Dispose();
            throw new InvalidOperationException("Missing SHA-256 metadata on downloaded object.");
        }

        var metadata = new VersionedObjectMetadata(
            response.VersionId!,
            response.ETag?.Trim('"') ?? string.Empty,
            sha256,
            response.LastModified.ToUniversalTime(),
            response.ContentLength);

        return new VersionedDownloadResult(metadata, new DownloadStreamWrapper(response));
    }

    private async Task<VersionedObjectMetadata> GetObjectMetadataAsync(CloudObjectIdentifier identifier, string versionId, CancellationToken cancellationToken)
    {
        var request = new GetObjectMetadataRequest
        {
            BucketName = identifier.BucketName,
            Key = identifier.ObjectKey,
            VersionId = versionId
        };

        var response = await _s3.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
        var sha256 = ResolveSha256(response.Metadata);
        if (string.IsNullOrEmpty(sha256))
        {
            throw new InvalidOperationException("Missing SHA-256 metadata on stored object.");
        }

        return new VersionedObjectMetadata(
            versionId,
            response.ETag?.Trim('"') ?? string.Empty,
            sha256,
            response.LastModified.ToUniversalTime(),
            response.ContentLength);
    }

    private static string ResolveSha256(MetadataCollection metadata)
    {
        foreach (var key in metadata.Keys)
        {
            if (string.Equals(key, "x-amz-meta-sha256", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "sha256", StringComparison.OrdinalIgnoreCase))
            {
                return metadata[key];
            }
        }

        return string.Empty;
    }

    private async Task<string> ResolveSha256Async(CloudObjectIdentifier identifier, string versionId, CancellationToken cancellationToken)
    {
        var request = new GetObjectMetadataRequest
        {
            BucketName = identifier.BucketName,
            Key = identifier.ObjectKey,
            VersionId = versionId
        };

        var response = await _s3.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
        var sha256 = ResolveSha256(response.Metadata);
        if (string.IsNullOrEmpty(sha256))
        {
            throw new InvalidOperationException("Missing SHA-256 metadata on stored object.");
        }

        return sha256;
    }
}

internal sealed class DownloadStreamWrapper : Stream
{
    private readonly GetObjectResponse _response;

    public DownloadStreamWrapper(GetObjectResponse response)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
    }

    private Stream InnerStream => _response.ResponseStream;

    public override bool CanRead => InnerStream.CanRead;

    public override bool CanSeek => InnerStream.CanSeek;

    public override bool CanWrite => InnerStream.CanWrite;

    public override long Length => InnerStream.Length;

    public override long Position
    {
        get => InnerStream.Position;
        set => InnerStream.Position = value;
    }

    public override void Flush() => InnerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => InnerStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => InnerStream.Seek(offset, origin);

    public override void SetLength(long value) => InnerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => InnerStream.Write(buffer, offset, count);

    public override async ValueTask DisposeAsync()
    {
        await InnerStream.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InnerStream.Dispose();
            _response.Dispose();
        }

        base.Dispose(disposing);
    }
}

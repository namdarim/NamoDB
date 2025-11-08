using Amazon.S3;
using Amazon.S3.Model;
using Namo.Domain;

namespace Namo.Infrastructure;

public sealed class S3VersionedObjectClient : IVersionedObjectClient
{
    private readonly IAmazonS3 _s3;

    public S3VersionedObjectClient(IAmazonS3 s3)
    {
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
    }

    public async Task<VersionedObjectMetadata?> GetLatestAsync(string bucketName, string objectKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(objectKey);

        var request = new ListObjectVersionsRequest
        {
            BucketName = bucketName,
            Prefix = objectKey,
            MaxKeys = 20
        };

        do
        {
            var response = await _s3.ListObjectVersionsAsync(request, cancellationToken).ConfigureAwait(false);
            var version = response.Versions
                .Where(v => string.Equals(v.Key, objectKey, StringComparison.Ordinal) && v.IsLatest)
                .OrderByDescending(v => v.LastModified)
                .FirstOrDefault();
            if (version != null && !string.IsNullOrEmpty(version.VersionId))
            {
                return await GetVersionMetadataAsync(bucketName, objectKey, version.VersionId, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(response.NextVersionIdMarker))
            {
                request.VersionIdMarker = response.NextVersionIdMarker;
                request.KeyMarker = response.NextKeyMarker;
            }
            else
            {
                break;
            }
        }
        while (true);

        return null;
    }

    public async Task DownloadVersionAsync(string bucketName, string objectKey, string versionId, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(objectKey);
        ArgumentException.ThrowIfNullOrEmpty(versionId);
        ArgumentNullException.ThrowIfNull(destination);

        var request = new GetObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            VersionId = versionId
        };

        using var response = await _s3.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        await response.ResponseStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VersionedObjectMetadata> UploadSnapshotAsync(string bucketName, string objectKey, Stream content, string sha256Hex, DateTimeOffset createdAtUtc, IReadOnlyDictionary<string, string>? additionalMetadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(objectKey);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNullOrEmpty(sha256Hex);

        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            InputStream = content,
            AutoCloseStream = false
        };

        putRequest.Metadata.Add("sha256", sha256Hex);
        putRequest.Metadata.Add("created-at-utc", createdAtUtc.UtcDateTime.ToString("O"));
        if (additionalMetadata != null)
        {
            foreach (var pair in additionalMetadata)
            {
                putRequest.Metadata[pair.Key] = pair.Value;
            }
        }

        var putResponse = await _s3.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);
        var versionId = putResponse.VersionId ?? throw new InvalidOperationException("Upload did not return a VersionId.");

        return await GetVersionMetadataAsync(bucketName, objectKey, versionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<VersionedObjectMetadata> GetVersionMetadataAsync(string bucketName, string objectKey, string versionId, CancellationToken cancellationToken)
    {
        var headRequest = new GetObjectMetadataRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            VersionId = versionId
        };

        var metadataResponse = await _s3.GetObjectMetadataAsync(headRequest, cancellationToken).ConfigureAwait(false);
        return new VersionedObjectMetadata(
            versionId,
            metadataResponse.ETag?.Trim('"') ?? string.Empty,
            metadataResponse.LastModified.ToUniversalTime(),
            metadataResponse.ContentLength,
            ExtractSha256(metadataResponse.Metadata));
    }

    private static string? ExtractSha256(MetadataCollection metadata)
    {
        if (metadata.TryGetValue("x-amz-meta-sha256", out var sha))
        {
            return sha?.ToUpperInvariant();
        }

        if (metadata.TryGetValue("sha256", out var altSha))
        {
            return altSha?.ToUpperInvariant();
        }

        return null;
    }
}

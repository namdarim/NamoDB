using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Namo.Domain.DBSync;

namespace Namo.Infrastructure.DBSync;

/// <summary>
/// Minimal S3 helper for versioned objects (GET/PUT). No environment policy.
/// </summary>
public sealed class S3StorageService : IDisposable
{
    private readonly IAmazonS3 _s3;

    public S3StorageService(S3Settings settings)
    {
        _s3 = new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config { ServiceURL = settings.ServiceUrl, ForcePathStyle = settings.ForcePathStyle });
    }

    /// <summary>Get latest non-deleted version metadata. Throws if none or deleted.</summary>
    public async Task<ObjectVersionInfo> GetLatestVersionAsync(string bucket, string key, CancellationToken ct = default)
    {
        var req = new ListVersionsRequest { BucketName = bucket, Prefix = key };

        ListVersionsResponse resp;
        do
        {
            resp = await _s3.ListVersionsAsync(req, ct).ConfigureAwait(false);

            var tip = resp.Versions?.FirstOrDefault(v => v.Key == key && v.IsLatest == true);
            if (tip != null)
            {
                if (tip.IsDeleteMarker == true)
                    throw new DeletedObjectException("Object has a delete marker at the top.");

                return await GetObjectVersionInfo(bucket, key, tip.VersionId ?? throw new InvalidOperationException("Missing VersionId"), ct)
                    .ConfigureAwait(false);
            }

            req.KeyMarker = resp.NextKeyMarker;
            req.VersionIdMarker = resp.NextVersionIdMarker;

        } while (resp.IsTruncated == true);

        throw new NoRemoteVersionException($"No versions for {bucket}/{key}");
    }

    /// <summary>Fetch the exact object version and write it to the given file path (no replace, no backup).</summary>
    public async Task FetchObjectVersionToFileAsync(string bucket, string key, string versionId, string destinationFilePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);

        using var resp = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = key,
            VersionId = versionId
        }, ct).ConfigureAwait(false);

        await using var fs = File.Create(destinationFilePath);
        await resp.ResponseStream.CopyToAsync(fs, ct).ConfigureAwait(false);
    }

    /// <summary>Upload a local file as the next version for the key; return its version metadata.</summary>
    public async Task<ObjectVersionInfo> UploadObjectAsync(string bucket, string key, string filePath, CancellationToken ct = default)
    {
        var resp = await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            FilePath = filePath,
            ChecksumSHA256 = FileHash.Sha256OfFile(filePath)
        }, ct).ConfigureAwait(false);

        return await GetObjectVersionInfo(
            bucket,
            key,
            resp.VersionId ?? throw new InvalidOperationException("Missing VersionId"),
            ct
        ).ConfigureAwait(false);
    }

    private async Task<ObjectVersionInfo> GetObjectVersionInfo(string bucket, string key, string versionId, CancellationToken ct = default)
    {
        var response = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = bucket,
            Key = key,
            VersionId = versionId
        }, ct).ConfigureAwait(false);

        return new ObjectVersionInfo(
            VersionId: response.VersionId,
            ETag: response.ETag,
            ContentLength: response.ContentLength,
            LastModifiedUtc: response.LastModified?.ToUniversalTime()
                ?? throw new InvalidOperationException("LastModified should not be null.")
        );
    }

    public void Dispose()
    {
        if (_s3 is IDisposable d) d.Dispose();
    }
}

public readonly record struct ObjectVersionInfo(
    string VersionId,
    string ETag,
    long ContentLength,
    DateTimeOffset LastModifiedUtc
);

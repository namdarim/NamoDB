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

        do
        {
            var resp = await _s3.ListVersionsAsync(req, ct).ConfigureAwait(false);
            var tip = resp.Versions?.FirstOrDefault(v => v.Key == key && v.IsLatest == true);
            if (tip != null)
            {
                if (tip.IsDeleteMarker == true) throw new DeletedObjectException("object has a delete-marker on top.");

                var head = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucket,
                    Key = key,
                    VersionId = tip.VersionId
                }, ct).ConfigureAwait(false);

                var etag = (head.ETag ?? string.Empty).Trim('"');
                var len = head.ContentLength;
                var lm = head.LastModified?.ToUniversalTime() ?? tip.LastModified?.ToUniversalTime() ?? DateTime.UtcNow;

                var vid = tip.VersionId ?? throw new InvalidOperationException("missing VersionId");
                return new ObjectVersionInfo(vid, etag, len, new DateTimeOffset(lm, TimeSpan.Zero));
            }

            req.KeyMarker = resp.NextKeyMarker;
            req.VersionIdMarker = resp.NextVersionIdMarker;

        } while (!string.IsNullOrEmpty(req.KeyMarker) || !string.IsNullOrEmpty(req.VersionIdMarker));

        throw new NoRemoteVersionException($"no versions for {bucket}/{key}");
    }

    public async Task<int?> GetVersionRankAsync(string bucket, string key, string versionId, CancellationToken ct = default)
    {
        var req = new ListVersionsRequest { BucketName = bucket, Prefix = key };
        var rank = 0;
        do
        {
            var resp = await _s3.ListVersionsAsync(req, ct).ConfigureAwait(false);
            if (resp.Versions == null) break;
            foreach (var v in resp.Versions.Where(v => v.Key == key))
            {
                if (v.IsDeleteMarker == true) continue;
                if (v.VersionId == versionId) return rank;
                rank++;
            }
            req.KeyMarker = resp.NextKeyMarker;
            req.VersionIdMarker = resp.NextVersionIdMarker;
        }
        while (!string.IsNullOrEmpty(req.KeyMarker) || !string.IsNullOrEmpty(req.VersionIdMarker));

        return null;
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

    // inside S3StorageService
    private static FileStream OpenReadWithRetry(string path, int retries = 8, int delayMs = 120)
    {
        for (int i = 0; ; i++)
        {
            try
            {
                return new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, // tolerate indexers/scanners
                    bufferSize: 128 * 1024,
                    options: FileOptions.SequentialScan);
            }
            catch (IOException) when (i < retries)
            {
                Thread.Sleep(delayMs);
                continue;
            }
        }
    }


    /// <summary>Upload a local file as the next version for the key; return its version metadata.</summary>
    public async Task<ObjectVersionInfo> UploadObjectAsync(string bucket, string key, string filePath, CancellationToken ct = default)
    {
        await using var fs = OpenReadWithRetry(filePath);

        var put = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = fs,
            AutoCloseStream = false // we control disposal with await using
        };

        var resp = await _s3.PutObjectAsync(put, ct).ConfigureAwait(false);

        //var resp = await _s3.PutObjectAsync(new PutObjectRequest
        //{
        //    BucketName = bucket,
        //    Key = key,
        //    FilePath = filePath
        //}, ct).ConfigureAwait(false);

        var versionId = resp.VersionId;
        if (string.IsNullOrEmpty(versionId))
            throw new InvalidOperationException();

        var head = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = bucket,
            Key = key,
            VersionId = versionId
        }, ct).ConfigureAwait(false);

        var etag = (head.ETag ?? string.Empty).Trim('"');
        var len = head.ContentLength;
        var lm = head.LastModified?.ToUniversalTime() ?? DateTime.UtcNow;

        return new ObjectVersionInfo(versionId, etag, len, new DateTimeOffset(lm, TimeSpan.Zero));
    }

    public void Dispose() { if (_s3 is IDisposable d) d.Dispose(); }
}

public readonly record struct ObjectVersionInfo(
    string VersionId,
    string ETag,
    long ContentLength,
    DateTimeOffset LastModifiedUtc
);

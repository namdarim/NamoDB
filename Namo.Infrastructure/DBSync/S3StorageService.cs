using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Namo.Domain.DBSync;
using System.Security.Cryptography;
using System.Text;

namespace Namo.Infrastructure.DBSync;

// Focused S3 helper for versioned GET/PUT and manifest-friendly metadata.
public sealed class S3StorageService : IDisposable
{
    private readonly IAmazonS3 _s3;

    public S3StorageService(S3Settings settings)
    {
        _s3 = CreateClient(settings.AccessKey, settings.SecretKey, settings.ServiceUrl, settings.ForcePathStyle);
    }

    private IAmazonS3 CreateClient(string accessKey, string secretKey, string serviceUrl, bool forcePathStyle)
    {
        var creds = new BasicAWSCredentials(accessKey, secretKey);
        var cfg = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = forcePathStyle
        };
        return new AmazonS3Client(creds, cfg);
    }


    public async Task<(string VersionId, string Sha256, string ETag, long ContentLength, DateTimeOffset LastModifiedUtc)?>
           GetLatestTipAsync(string bucket, string key, CancellationToken ct = default)
    {
        var req = new ListVersionsRequest { BucketName = bucket, Prefix = key };

        do
        {
            var resp = await _s3.ListVersionsAsync(req, ct).ConfigureAwait(false);

            var tip = resp.Versions.FirstOrDefault(v => v.Key == key && v.IsLatest == true);
            if (tip != null)
            {
                if (tip.IsDeleteMarker == true)
                    throw new DeletedObjectException("object is deleted (delete-marker on top).");

                var head = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucket,
                    Key = key,
                    VersionId = tip.VersionId
                }, ct).ConfigureAwait(false);

                var etag = (head.ETag ?? string.Empty).Trim('"');
                var len = head.ContentLength;
                var lm = head.LastModified?.ToUniversalTime() ?? tip.LastModified?.ToUniversalTime() ?? DateTime.UtcNow;

                var vid = tip.VersionId ?? throw new InvalidOperationException("VersionId missing for latest tip.");
                return (vid, vid /* sha256 := versionId */, etag, len, new DateTimeOffset(lm, TimeSpan.Zero));
            }

            req.KeyMarker = resp.NextKeyMarker;
            req.VersionIdMarker = resp.NextVersionIdMarker;

        } while (!string.IsNullOrEmpty(req.KeyMarker) || !string.IsNullOrEmpty(req.VersionIdMarker));

        return null;
    }

    public async Task<int?> FindVersionRankAsync(string bucket, string key, string versionId, CancellationToken ct = default)
    {
        var req = new ListVersionsRequest { BucketName = bucket, Prefix = key };
        var rank = 0;

        do
        {
            var resp = await _s3.ListVersionsAsync(req, ct).ConfigureAwait(false);

            foreach (var v in resp.Versions.Where(v => v.Key == key))
            {
                if (v.IsDeleteMarker == true) continue;
                if (v.VersionId == versionId) return rank;
                rank++;
            }

            req.KeyMarker = resp.NextKeyMarker;
            req.VersionIdMarker = resp.NextVersionIdMarker;

        } while (!string.IsNullOrEmpty(req.KeyMarker) || !string.IsNullOrEmpty(req.VersionIdMarker));

        return null;
    }

    public async Task DownloadVersionToFileAsync(string bucket, string key, string versionId, string destPath, CancellationToken ct = default)
    {
        var tempPath = destPath + ".download.tmp";

        using (var resp = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = key,
            VersionId = versionId
        }, ct).ConfigureAwait(false))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await using var fs = File.Create(tempPath);
            await resp.ResponseStream.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        AtomicReplace(tempPath, destPath);
    }

    public async Task<(string VersionId, string Sha256, string ETag, long ContentLength, DateTimeOffset LastModifiedUtc)>
        UploadSnapshotAsync(string bucket, string key, string filePath, IDictionary<string, string>? extraMeta, CancellationToken ct = default)
    {
        var put = new PutObjectRequest { BucketName = bucket, Key = key, FilePath = filePath };
        if (extraMeta != null) foreach (var kv in extraMeta) put.Metadata[kv.Key] = kv.Value;

        var resp = await _s3.PutObjectAsync(put, ct).ConfigureAwait(false);
        var versionId = resp.VersionId;
        if (string.IsNullOrEmpty(versionId))
        {
            var latest = await GetLatestTipAsync(bucket, key, ct).ConfigureAwait(false)
                         ?? throw new InvalidOperationException("Unable to determine VersionId after upload.");
            versionId = latest.VersionId;
        }

        var head = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = bucket,
            Key = key,
            VersionId = versionId
        }, ct).ConfigureAwait(false);

        var etag = (head.ETag ?? string.Empty).Trim('"');
        var len = head.ContentLength;
        var lm = head.LastModified?.ToUniversalTime() ?? DateTime.UtcNow;

        return (versionId, versionId /* sha256 := versionId */, etag, len, new DateTimeOffset(lm, TimeSpan.Zero));
    }

    private static void AtomicReplace(string tempPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (OperatingSystem.IsWindows())
        {
            if (File.Exists(targetPath))
            {
                var backup = targetPath + ".bak";
                try { File.Replace(tempPath, targetPath, backup, ignoreMetadataErrors: true); }
                finally { if (File.Exists(backup)) File.Delete(backup); }
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }
        else
        {
            File.Move(tempPath, targetPath, overwrite: true);
        }
    }

    public void Dispose()
    {
        if (_s3 is IDisposable d) d.Dispose();
    }

}


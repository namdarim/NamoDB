using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Namo.Models;

namespace Namo.S3;

public sealed class S3SnapshotCloudClient : ISnapshotCloudClient
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _objectKey;
    private readonly string _downloadDirectory;

    public S3SnapshotCloudClient(IAmazonS3 s3Client, string bucketName, string objectKey, string? downloadDirectory = null)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = !string.IsNullOrWhiteSpace(bucketName) ? bucketName : throw new ArgumentException("Value cannot be null or whitespace.", nameof(bucketName));
        _objectKey = !string.IsNullOrWhiteSpace(objectKey) ? objectKey : throw new ArgumentException("Value cannot be null or whitespace.", nameof(objectKey));
        _downloadDirectory = string.IsNullOrWhiteSpace(downloadDirectory) ? Path.GetTempPath() : downloadDirectory!;
        Directory.CreateDirectory(_downloadDirectory);
    }

    public async Task<SnapshotVersionInfo?> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = _objectKey
            };

            var response = await _s3Client.GetObjectMetadataAsync(request, cancellationToken).ConfigureAwait(false);
            return new SnapshotVersionInfo(
                response.VersionId ?? throw new InvalidOperationException("VersionId is required."),
                response.ETag ?? string.Empty,
                response.Headers.ContentLength,
                response.LastModified.ToUniversalTime(),
                TryGetMetadataValue(response.Metadata, "sha256"));
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<SnapshotDownloadResult> DownloadVersionAsync(string versionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(versionId));
        }

        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = _objectKey,
            VersionId = versionId
        };

        var response = await _s3Client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        var version = new SnapshotVersionInfo(
            response.VersionId ?? versionId,
            response.ETag ?? string.Empty,
            response.ContentLength,
            response.LastModified.ToUniversalTime(),
            TryGetMetadataValue(response.Metadata, "sha256"));

        var tempFilePath = Path.Combine(_downloadDirectory, $"{Guid.NewGuid():N}.tmp");

        await using (var fileStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await response.ResponseStream.CopyToAsync(fileStream, 81920, cancellationToken).ConfigureAwait(false);
        }

        return new SnapshotDownloadResult(version, tempFilePath);
    }

    public async Task<SnapshotUploadResult> UploadSnapshotAsync(SnapshotUploadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!File.Exists(request.SnapshotFilePath))
        {
            throw new FileNotFoundException("Snapshot file not found.", request.SnapshotFilePath);
        }

        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = _objectKey,
            FilePath = request.SnapshotFilePath
        };

        foreach (var pair in request.Metadata)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
            {
                putRequest.Metadata[pair.Key] = pair.Value;
            }
        }

        var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);
        return new SnapshotUploadResult(response.VersionId ?? throw new InvalidOperationException("VersionId is required."), response.ETag ?? string.Empty);
    }

    private static string? TryGetMetadataValue(MetadataCollection metadata, string key)
    {
        if (metadata == null)
        {
            return null;
        }

        return metadata.TryGetValue(key, out var value) ? value : null;
    }
}

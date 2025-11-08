using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Namo.Models;
using Namo.S3;
using Namo.Sqlite;
using Namo.Storage;

namespace Namo.Sync;

public sealed class DatabaseSyncOrchestrator
{
    private readonly ISnapshotCloudClient _cloudClient;
    private readonly ISqliteSnapshotApplier _snapshotApplier;
    private readonly ISqliteSnapshotProvider _snapshotProvider;
    private readonly IVersionMetadataStore _versionStore;
    private readonly string _snapshotCachePath;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly Func<DateTimeOffset> _clock;

    public DatabaseSyncOrchestrator(
        ISnapshotCloudClient cloudClient,
        ISqliteSnapshotApplier snapshotApplier,
        ISqliteSnapshotProvider snapshotProvider,
        IVersionMetadataStore versionStore,
        string snapshotCachePath,
        Func<DateTimeOffset>? clock = null)
    {
        _cloudClient = cloudClient ?? throw new ArgumentNullException(nameof(cloudClient));
        _snapshotApplier = snapshotApplier ?? throw new ArgumentNullException(nameof(snapshotApplier));
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _versionStore = versionStore ?? throw new ArgumentNullException(nameof(versionStore));
        _snapshotCachePath = !string.IsNullOrWhiteSpace(snapshotCachePath) ? snapshotCachePath : throw new ArgumentException("Value cannot be null or whitespace.", nameof(snapshotCachePath));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        var directory = Path.GetDirectoryName(_snapshotCachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<bool> SyncAsync(CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var latest = await _cloudClient.GetLatestVersionAsync(cancellationToken).ConfigureAwait(false);
            if (latest is null)
            {
                return false;
            }

            var current = await _versionStore.GetAsync(cancellationToken).ConfigureAwait(false);
            if (current is not null)
            {
                if (string.Equals(current.VersionId, latest.VersionId, StringComparison.Ordinal))
                {
                    return false;
                }

                if (latest.LastModified <= current.AppliedAtUtc)
                {
                    return false;
                }
            }

            await using var download = await _cloudClient.DownloadVersionAsync(latest.VersionId, cancellationToken).ConfigureAwait(false);

            var computedSha = ComputeSha256(download.TempFilePath);

            if (!string.IsNullOrWhiteSpace(download.Version.Sha256) && !string.Equals(download.Version.Sha256, computedSha, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Downloaded snapshot hash mismatch.");
            }

            var snapshotDirectory = Path.GetDirectoryName(_snapshotCachePath);
            if (!string.IsNullOrEmpty(snapshotDirectory))
            {
                Directory.CreateDirectory(snapshotDirectory);
            }

            var stagingPath = _snapshotCachePath + ".tmp";
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }

            try
            {
                File.Move(download.TempFilePath, stagingPath);
                File.Move(stagingPath, _snapshotCachePath, true);

                await _snapshotApplier.ApplySnapshotAsync(_snapshotCachePath, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (File.Exists(stagingPath))
                    {
                        File.Delete(stagingPath);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }
            }

            var appliedVersion = new AppliedVersionInfo(
                download.Version.VersionId,
                download.Version.ETag,
                computedSha,
                new FileInfo(_snapshotCachePath).Length,
                _clock());

            await _versionStore.SetAsync(appliedVersion, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<SnapshotUploadResult> PublishAsync(string? appBuild, CancellationToken cancellationToken)
    {
        await _publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshotPath = Path.Combine(Path.GetDirectoryName(_snapshotCachePath) ?? Path.GetTempPath(), $"publish-{Guid.NewGuid():N}.db");
            try
            {
                await _snapshotProvider.CreateSnapshotAsync(snapshotPath, cancellationToken).ConfigureAwait(false);

                var sha = ComputeSha256(snapshotPath);
                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sha256"] = sha,
                    ["created-at-utc"] = _clock().ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                };

                if (!string.IsNullOrWhiteSpace(appBuild))
                {
                    metadata["app-build"] = appBuild!;
                }

                var uploadRequest = new SnapshotUploadRequest(snapshotPath, metadata);
                var result = await _cloudClient.UploadSnapshotAsync(uploadRequest, cancellationToken).ConfigureAwait(false);
                return result;
            }
            finally
            {
                try
                {
                    if (File.Exists(snapshotPath))
                    {
                        File.Delete(snapshotPath);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}

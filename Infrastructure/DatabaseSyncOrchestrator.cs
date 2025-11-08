using System.Security.Cryptography;
using Namo.Domain;

namespace Namo.Infrastructure;

public sealed class DatabaseSyncOrchestrator : IDatabaseSyncOrchestrator
{
    private readonly IVersionedObjectClient _versionedObjectClient;
    private readonly ISnapshotDownloadProvider _snapshotDownloadProvider;
    private readonly ISqliteSnapshotFactory _snapshotFactory;
    private readonly ILocalDatabaseApplier _databaseApplier;
    private readonly IAppliedVersionStore _appliedVersionStore;
    private readonly IAppPathProvider _pathProvider;
    private readonly DatabaseSyncOrchestratorOptions _options;

    public DatabaseSyncOrchestrator(
        IVersionedObjectClient versionedObjectClient,
        ISnapshotDownloadProvider snapshotDownloadProvider,
        ISqliteSnapshotFactory snapshotFactory,
        ILocalDatabaseApplier databaseApplier,
        IAppliedVersionStore appliedVersionStore,
        IAppPathProvider pathProvider,
        DatabaseSyncOrchestratorOptions? options = null)
    {
        _versionedObjectClient = versionedObjectClient ?? throw new ArgumentNullException(nameof(versionedObjectClient));
        _snapshotDownloadProvider = snapshotDownloadProvider ?? throw new ArgumentNullException(nameof(snapshotDownloadProvider));
        _snapshotFactory = snapshotFactory ?? throw new ArgumentNullException(nameof(snapshotFactory));
        _databaseApplier = databaseApplier ?? throw new ArgumentNullException(nameof(databaseApplier));
        _appliedVersionStore = appliedVersionStore ?? throw new ArgumentNullException(nameof(appliedVersionStore));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _options = options ?? DatabaseSyncOrchestratorOptions.Default;
    }

    public async Task<PublishResult> PublishAsync(string liveDatabasePath, string bucketName, string objectKey, CancellationToken cancellationToken)
    {
        var tempDirectory = _pathProvider.GetTemporaryDirectory();
        var snapshotFileName = $"{Guid.NewGuid():N}.snapshot";
        var snapshotPath = Path.Combine(tempDirectory, snapshotFileName);

        var snapshot = await _snapshotFactory.CreateSnapshotAsync(liveDatabasePath, snapshotPath, cancellationToken).ConfigureAwait(false);
        var sha256 = await ComputeSha256Async(snapshot, cancellationToken).ConfigureAwait(false);

        await using var uploadStream = new FileStream(snapshot, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        var metadata = await _versionedObjectClient.UploadSnapshotAsync(
            bucketName,
            objectKey,
            uploadStream,
            sha256,
            DateTimeOffset.UtcNow,
            _options.AdditionalUploadMetadata,
            cancellationToken).ConfigureAwait(false);

        return new PublishResult(metadata, snapshot);
    }

    public async Task<SyncResult> SyncAsync(string bucketName, string objectKey, string liveDatabasePath, string snapshotWorkingPath, CancellationToken cancellationToken)
    {
        var scope = BuildScope(bucketName, objectKey);
        var current = await _appliedVersionStore.GetAsync(scope, cancellationToken).ConfigureAwait(false);
        var latest = await _versionedObjectClient.GetLatestAsync(bucketName, objectKey, cancellationToken).ConfigureAwait(false);
        if (latest == null)
        {
            return new SyncResult(false, null);
        }

        if (current != null)
        {
            if (string.Equals(current.VersionId, latest.VersionId, StringComparison.Ordinal))
            {
                return new SyncResult(false, latest);
            }

            if (latest.LastModifiedUtc <= current.CloudLastModifiedUtc)
            {
                throw new InvalidOperationException("Detected potential rollback attempt. Aborting sync.");
            }
        }

        if (string.IsNullOrWhiteSpace(latest.Sha256Hex))
        {
            throw new InvalidOperationException("Cloud snapshot metadata is missing SHA-256.");
        }

        var directory = Path.GetDirectoryName(snapshotWorkingPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(snapshotWorkingPath))
        {
            File.Delete(snapshotWorkingPath);
        }

        var attempts = Math.Max(0, _options.IntegrityRetryCount) + 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _snapshotDownloadProvider.DownloadAsync(
                    (stream, token) => _versionedObjectClient.DownloadVersionAsync(bucketName, objectKey, latest.VersionId, stream, token),
                    snapshotWorkingPath,
                    latest.Sha256Hex,
                    cancellationToken).ConfigureAwait(false);

                var fileSize = new FileInfo(snapshotWorkingPath).Length;
                if (fileSize != latest.Size)
                {
                    throw new InvalidDataException("Downloaded snapshot size mismatch.");
                }

                await _databaseApplier.ApplySnapshotAsync(liveDatabasePath, snapshotWorkingPath, _options.ApplyOptions, cancellationToken).ConfigureAwait(false);

                var appliedInfo = new AppliedVersionInfo(
                    latest.VersionId,
                    latest.ETag,
                    latest.Sha256Hex,
                    latest.Size,
                    DateTimeOffset.UtcNow,
                    latest.LastModifiedUtc);

                await _appliedVersionStore.SetAsync(scope, appliedInfo, cancellationToken).ConfigureAwait(false);
                return new SyncResult(true, latest);
            }
            catch (InvalidDataException) when (attempt + 1 < attempts)
            {
                await Task.Delay(_options.IntegrityRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidDataException("Failed to apply snapshot after retries.");
    }

    private static string BuildScope(string bucketName, string objectKey)
        => $"{bucketName}/{objectKey}";

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}

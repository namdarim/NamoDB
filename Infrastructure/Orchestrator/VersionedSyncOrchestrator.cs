using System.IO;
using Microsoft.Extensions.Options;
using Namo.Domain.Contracts.Cloud;
using Namo.Domain.Contracts.Sync;
using Namo.Infrastructure.Sync.Apply;

namespace Namo.Infrastructure.Orchestrator;

public sealed class VersionedSyncOrchestrator : ISyncOrchestrator
{
    private readonly ISqliteSnapshotCreator _snapshotCreator;
    private readonly IVersionedObjectClient _objectClient;
    private readonly ISnapshotDownloader _snapshotDownloader;
    private readonly ISqliteSnapshotApplier _snapshotApplier;
    private readonly IVersionInfoStore _versionInfoStore;
    private readonly SyncOrchestratorOptions _options;

    public VersionedSyncOrchestrator(
        ISqliteSnapshotCreator snapshotCreator,
        IVersionedObjectClient objectClient,
        ISnapshotDownloader snapshotDownloader,
        ISqliteSnapshotApplier snapshotApplier,
        IVersionInfoStore versionInfoStore,
        IOptions<SyncOrchestratorOptions> options)
    {
        _snapshotCreator = snapshotCreator ?? throw new ArgumentNullException(nameof(snapshotCreator));
        _objectClient = objectClient ?? throw new ArgumentNullException(nameof(objectClient));
        _snapshotDownloader = snapshotDownloader ?? throw new ArgumentNullException(nameof(snapshotDownloader));
        _snapshotApplier = snapshotApplier ?? throw new ArgumentNullException(nameof(snapshotApplier));
        _versionInfoStore = versionInfoStore ?? throw new ArgumentNullException(nameof(versionInfoStore));
        _options = options?.Value ?? new SyncOrchestratorOptions();
    }

    public async Task<VersionedUploadResult> PublishAsync(string liveDatabasePath, CloudObjectIdentifier identifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(liveDatabasePath);
        ArgumentNullException.ThrowIfNull(identifier);

        var temporarySnapshotPath = Path.Combine(Path.GetTempPath(), $"snapshot-{Guid.NewGuid():N}.db");
        try
        {
            var snapshot = await _snapshotCreator.CreateSnapshotAsync(liveDatabasePath, temporarySnapshotPath, cancellationToken).ConfigureAwait(false);
            await using var stream = new FileStream(snapshot.SnapshotPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var createdAtUtc = DateTimeOffset.UtcNow;
            var result = await _objectClient.UploadSnapshotAsync(identifier, stream, snapshot.Sha256Hex, createdAtUtc, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            if (File.Exists(temporarySnapshotPath))
            {
                File.Delete(temporarySnapshotPath);
            }
        }
    }

    public async Task<SyncResult> SyncAsync(string liveDatabasePath, string snapshotTempPath, CloudObjectIdentifier identifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(liveDatabasePath);
        ArgumentException.ThrowIfNullOrEmpty(snapshotTempPath);
        ArgumentNullException.ThrowIfNull(identifier);

        var latest = await _objectClient.GetLatestVersionAsync(identifier, cancellationToken).ConfigureAwait(false);
        if (latest is null)
        {
            var existing = await _versionInfoStore.ReadAsync(cancellationToken).ConfigureAwait(false);
            return new SyncResult(false, existing);
        }

        var applied = await _versionInfoStore.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (applied is not null && string.Equals(applied.Metadata.VersionId, latest.VersionId, StringComparison.Ordinal))
        {
            return new SyncResult(false, applied);
        }

        if (applied is not null && latest.LastModifiedUtc < applied.AppliedAtUtc)
        {
            throw new InvalidOperationException("Anti-rollback policy rejected an older version.");
        }

        var downloaded = await _snapshotDownloader.DownloadAsync(identifier, latest, snapshotTempPath, cancellationToken).ConfigureAwait(false);
        try
        {
            var options = new SqliteSnapshotApplierOptions(_options.CreateRollbackCopy, _options.RollbackCopyPath);
            await _snapshotApplier.ApplySnapshotAsync(liveDatabasePath, downloaded.SnapshotPath, options, cancellationToken).ConfigureAwait(false);

            var updated = new AppliedVersionInfo(identifier, latest, downloaded.Sha256Hex, DateTimeOffset.UtcNow);
            await _versionInfoStore.WriteAsync(updated, cancellationToken).ConfigureAwait(false);

            return new SyncResult(true, updated);
        }
        finally
        {
            if (File.Exists(snapshotTempPath))
            {
                File.Delete(snapshotTempPath);
            }
        }
    }
}

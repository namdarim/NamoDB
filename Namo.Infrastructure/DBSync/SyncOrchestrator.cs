using Namo.Domain.DBSync;

namespace Namo.Infrastructure.DBSync;

// Orchestrates Pull/Push, now able to snapshot the live SQLite DB via SqliteConnection.BackupDatabase.
public sealed class SyncOrchestrator
{
    private readonly S3StorageService _s3;
    private readonly S3Settings _settings;
    private readonly DbSyncManifestStore _manifestStore;

    public SyncOrchestrator(S3StorageService s3, S3Settings settings, DbSyncManifestStore manifestStore)
    {
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
    }

    public async Task PullAsync(
        string localDbPath,
        Func<CancellationToken, Task>? preReplaceHook = null,
        CancellationToken ct = default)
    {
        var manifest = await _manifestStore.LoadAsync(ct).ConfigureAwait(false)
                      ?? new DbSyncManifest { Bucket = _settings.Bucket, Key = _settings.Key };

        var tip = await _s3.GetLatestTipAsync(_settings.Bucket, _settings.Key, ct).ConfigureAwait(false);
        if (tip is null)
            throw new DeletedObjectException($"Object {_settings.Bucket}/{_settings.Key} has no versions.");

        var (latestVid, latestSha, latestETag, latestLen, latestLmUtc) = tip.Value;

        if (!manifest.IsEmpty() && string.Equals(manifest.VersionId, latestVid, StringComparison.Ordinal))
            return; // no-op

        if (!manifest.IsEmpty())
        {
            var idxLocal = await _s3.FindVersionRankAsync(_settings.Bucket, _settings.Key, manifest.VersionId, ct).ConfigureAwait(false);
            var idxCand = await _s3.FindVersionRankAsync(_settings.Bucket, _settings.Key, latestVid, ct).ConfigureAwait(false);

            if (idxLocal.HasValue && idxCand.HasValue && idxCand.Value > idxLocal.Value)
                throw new RollbackRejectedException($"Discovered version {latestVid} is older than local {manifest.VersionId}.");
        }

        if (preReplaceHook is not null) await preReplaceHook(ct).ConfigureAwait(false);

        await _s3.DownloadVersionToFileAsync(_settings.Bucket, _settings.Key, latestVid, localDbPath, ct).ConfigureAwait(false);

        manifest.Bucket = _settings.Bucket;
        manifest.Key = _settings.Key;
        manifest.VersionId = latestVid;
        manifest.Sha256 = latestSha;   // by policy: equals VersionId
        manifest.ETag = latestETag;
        manifest.ContentLength = latestLen;
        manifest.LastModifiedUtc = latestLmUtc;
        manifest.AppliedAtUtc = DateTimeOffset.UtcNow;

        await _manifestStore.SaveAsync(manifest, ct).ConfigureAwait(false);
    }

    // Push using an already-created snapshot file (kept for flexibility).
    public async Task PushAsync(
        string snapshotPath,
        IDictionary<string, string>? extraMeta = null,
        CancellationToken ct = default)
    {
        var manifest = await _manifestStore.LoadAsync(ct).ConfigureAwait(false)
                      ?? new DbSyncManifest { Bucket = _settings.Bucket, Key = _settings.Key, VersionId = string.Empty };

        var tip = await _s3.GetLatestTipAsync(_settings.Bucket, _settings.Key, ct).ConfigureAwait(false);
        if (tip is null)
            throw new DeletedObjectException($"Object {_settings.Bucket}/{_settings.Key} has no versions.");

        var remoteVid = tip.Value.VersionId;
        if (!string.Equals(manifest.VersionId, remoteVid, StringComparison.Ordinal))
            throw new ConflictException($"S3 latest ({remoteVid}) != local ({manifest.VersionId}). Pull first.");

        var uploaded = await _s3.UploadSnapshotAsync(_settings.Bucket, _settings.Key, snapshotPath, extraMeta, ct).ConfigureAwait(false);

        manifest.Bucket = _settings.Bucket;
        manifest.Key = _settings.Key;
        manifest.VersionId = uploaded.VersionId;
        manifest.Sha256 = uploaded.Sha256; // equals VersionId
        manifest.ETag = uploaded.ETag;
        manifest.ContentLength = uploaded.ContentLength;
        manifest.LastModifiedUtc = uploaded.LastModifiedUtc;
        manifest.AppliedAtUtc = DateTimeOffset.UtcNow;

        await _manifestStore.SaveAsync(manifest, ct).ConfigureAwait(false);
    }

    // NEW: Push directly from a live DB by creating a snapshot with SqliteConnection.BackupDatabase.
    public async Task PushFromLiveDbAsync(
        string liveDbPath,
        string snapshotsDir,
        IDictionary<string, string>? extraMeta = null,
        CancellationToken ct = default)
    {
        // Concurrency precondition: remote latest must match local manifest.
        var manifest = await _manifestStore.LoadAsync(ct).ConfigureAwait(false)
                      ?? new DbSyncManifest { Bucket = _settings.Bucket, Key = _settings.Key, VersionId = string.Empty };

        var tip = await _s3.GetLatestTipAsync(_settings.Bucket, _settings.Key, ct).ConfigureAwait(false);
        if (tip is null)
            throw new DeletedObjectException($"Object {_settings.Bucket}/{_settings.Key} has no versions.");

        var remoteVid = tip.Value.VersionId;
        if (!string.Equals(manifest.VersionId, remoteVid, StringComparison.Ordinal))
            throw new ConflictException($"S3 latest ({remoteVid}) != local ({manifest.VersionId}). Pull first.");

        // Build snapshot locally using SQLite backup API.
        var snapshotPath = SqliteBackup.CreateSnapshot(liveDbPath, snapshotsDir, snapshotFileName: null, verifyIntegrity: false);

        try
        {
            var uploaded = await _s3.UploadSnapshotAsync(_settings.Bucket, _settings.Key, snapshotPath, extraMeta, ct).ConfigureAwait(false);

            manifest.Bucket = _settings.Bucket;
            manifest.Key = _settings.Key;
            manifest.VersionId = uploaded.VersionId;
            manifest.Sha256 = uploaded.Sha256; // equals VersionId
            manifest.ETag = uploaded.ETag;
            manifest.ContentLength = uploaded.ContentLength;
            manifest.LastModifiedUtc = uploaded.LastModifiedUtc;
            manifest.AppliedAtUtc = DateTimeOffset.UtcNow;

            await _manifestStore.SaveAsync(manifest, ct).ConfigureAwait(false);
        }
        finally
        {
            // Best-effort cleanup of the temp snapshot.
            TryDelete(snapshotPath);
        }
    }

    // Convenience: publish from live DB, then pull to ensure local file parity.
    public async Task PushFromLiveDbThenPullAsync(
        string liveDbPath,
        string snapshotsDir,
        string localDbPath,
        IDictionary<string, string>? extraMeta = null,
        Func<CancellationToken, Task>? preReplaceHook = null,
        CancellationToken ct = default)
    {
        await PushFromLiveDbAsync(liveDbPath, snapshotsDir, extraMeta, ct).ConfigureAwait(false);
        await PullAsync(localDbPath, preReplaceHook, ct).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); }
        catch { /* ignore */ }
    }
}

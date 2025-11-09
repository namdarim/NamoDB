// Namo.Infrastructure.DBSync/SyncOrchestrator.cs
using Namo.Domain.DBSync;

namespace Namo.Infrastructure.DBSync;

/// <summary>
/// Orchestrates Pull/Push. Returns status codes instead of throwing for controllable conflicts.
/// Unexpected IO/network exceptions still surface as exceptions.
/// </summary>
public sealed class SyncOrchestrator
{
    private readonly S3StorageService _s3;
    private readonly S3Settings _settings;
    private readonly DbSyncManifestStore _manifestStore;
    private readonly IBackupNamer _backupNamer;

    public SyncOrchestrator(S3StorageService s3, S3Settings settings, DbSyncManifestStore manifestStore, IBackupNamer backupNamer)
    {
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        _backupNamer = backupNamer ?? throw new ArgumentNullException(nameof(backupNamer));
    }

    /// <summary>
    /// Pull latest remote version to localDbPath.
    /// - Detects local changes via hash.
    /// - If overwrite is needed and backupNamer != null, creates a descriptive backup (never overwrites).
    /// - Returns a SyncResult with a clear outcome.
    /// </summary>
    public async Task<SyncResult> PullAsync(
        string localDbPath,
        bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            var localChanged = false;
            var manifest = await _manifestStore.LoadAsync(ct).ConfigureAwait(false);
            if (File.Exists(localDbPath))
            {
                if (manifest == null || FileHash.Sha256OfFile(localDbPath) != manifest.ServerState.Sha256)
                    localChanged = true;
            }
            if (localChanged && !force)
                return new SyncResult(SyncAction.Pull, SyncOutcome.Conflict_LocalChanged, false,
                       Message: "Local content hash differs from last applied; use force to overwrite.");

            ObjectVersionInfo latest;
            try
            {
                latest = await _s3.GetLatestVersionAsync(_settings.Bucket, _settings.Key, ct).ConfigureAwait(false);
            }
            catch (DeletedObjectException)
            {
                return new SyncResult(SyncAction.Pull, SyncOutcome.RemoteDeleted, force,
                    Message: "Remote object is deleted or has no versions.");
            }
            catch (NoRemoteVersionException)
            {
                return new SyncResult(SyncAction.Pull, SyncOutcome.RemoteDeleted, force,
                    Message: "No remote versions exist.");
            }

            if (manifest?.ServerState.VersionId == latest.VersionId)
                return new SyncResult(SyncAction.Pull, SyncOutcome.NoChange, force);

            if (localChanged)

            // Prepare backup if we will overwrite and backupNamer is provided
            if (fileExists && backupNamer is not null &&
                (localModified || !string.Equals(manifest.VersionId, latest.VersionId, StringComparison.Ordinal)))
            {
                var ctxBackup = new BackupNamingContext
                {
                    LocalDbPath = localDbPath,
                    LocalContentSha256 = localHashBefore,
                    LocalVersionId = manifest.VersionId ?? string.Empty,
                    RemoteVersionId = latest.VersionId,
                    AppliedAtUtc = manifest.AppliedAtUtc,
                    NowUtc = DateTimeOffset.UtcNow,
                    Reason = "pull-overwrite"
                };

                var backupPath = backupNamer(ctxBackup);
                if (string.IsNullOrWhiteSpace(backupPath))
                    return new SyncResult(SyncAction.Pull, SyncOutcome.Failed, force, "Backup namer returned an empty path.");

                if (File.Exists(backupPath))
                {
                    return new SyncResult(SyncAction.Pull, SyncOutcome.BackupAlreadyExists, force,
                        Message: $"Backup already exists: {backupPath}",
                        LocalBackupPath: backupPath,
                        RemoteVersionIdBefore: latest.VersionId, LocalVersionIdBefore: manifest.VersionId,
                        LocalHashBefore: localHashBefore);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.Copy(localDbPath, backupPath, overwrite: false);
            }

            // Fetch to temp then atomic replace
            var tmp = localDbPath + ".download.tmp";
            try
            {
                await _s3.FetchObjectVersionToFileAsync(_settings.Bucket, _settings.Key, latest.VersionId, tmp, ct).ConfigureAwait(false);
                AtomicReplace(tmp, localDbPath);
            }
            finally { TryDelete(tmp); }

            var localHashAfter = FileHash.Sha256OfFile(localDbPath);

            // Update manifest
            manifest.Bucket = _settings.Bucket;
            manifest.Key = _settings.Key;
            manifest.VersionId = latest.VersionId;
            manifest.Sha256 = latest.VersionId; // policy: remote sha == versionId
            manifest.ETag = latest.ETag;
            manifest.ContentLength = latest.ContentLength;
            manifest.LastModifiedUtc = latest.LastModifiedUtc;
            manifest.AppliedAtUtc = DateTimeOffset.UtcNow;
            manifest.LocalContentSha256 = localHashAfter;

            await _manifestStore.SaveAsync(manifest, ct).ConfigureAwait(false);

            return new SyncResult(SyncAction.Pull, SyncOutcome.Replaced, force,
                RemoteVersionIdBefore: latest.VersionId,
                LocalVersionIdBefore: manifest.VersionId,
                LocalHashBefore: localHashBefore,
                LocalHashAfter: localHashAfter);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SyncResult(SyncAction.Pull, SyncOutcome.Failed, force, Message: ex.Message);
        }
    }

    /// <summary>
    /// Push from a file. Enforces head precondition unless forced.
    /// </summary>
    public async Task<SyncResult> PushFromFileAsync(string filePath, bool force = false, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                var message = string.IsNullOrWhiteSpace(filePath)
                    ? "File path is empty."
                    : $"File not found: {filePath}";
                return new SyncResult(SyncAction.Push, SyncOutcome.Failed, force, Message: message);
            }

            var manifest = await _manifestStore.LoadAsync(ct).ConfigureAwait(false)
                          ?? new DbSyncManifest { Bucket = _settings.Bucket, Key = _settings.Key, VersionId = string.Empty };

            ObjectVersionInfo latest;
            try
            {
                latest = await _s3.GetLatestVersionAsync(_settings.Bucket, _settings.Key, ct).ConfigureAwait(false);
            }
            catch (DeletedObjectException)
            {
                return new SyncResult(SyncAction.Push, SyncOutcome.RemoteDeleted, force,
                    Message: "Remote object is deleted or has no versions.");
            }
            catch (NoRemoteVersionException)
            {
                // Allow first publish even if manifest is empty.
                if (manifest.IsEmpty() || force == true)
                {
                    var up0 = await _s3.UploadObjectAsync(_settings.Bucket, _settings.Key, filePath, ct).ConfigureAwait(false);
                    await UpdateManifestAfterPublish(manifest, up0, filePath, ct).ConfigureAwait(false);
                    return new SyncResult(SyncAction.Push, SyncOutcome.Published, force,
                        RemoteVersionIdAfter: up0.VersionId, LocalHashAfter: FileHash.Sha256OfFile(filePath));
                }

                return new SyncResult(SyncAction.Push, SyncOutcome.Conflict_RemoteHeadMismatch, force,
                    Message: "No remote head while local manifest is not empty; force to publish.");
            }

            if (!string.Equals(manifest.VersionId, latest.VersionId, StringComparison.Ordinal) && !force)
            {
                return new SyncResult(SyncAction.Push, SyncOutcome.Conflict_RemoteHeadMismatch, false,
                    Message: $"Remote head {latest.VersionId} != local {manifest.VersionId}.",
                    RemoteVersionIdBefore: latest.VersionId, LocalVersionIdBefore: manifest.VersionId,
                    LocalHashBefore: FileHash.Sha256OfFile(filePath));
            }

            var uploaded = await _s3.UploadObjectAsync(_settings.Bucket, _settings.Key, filePath, ct).ConfigureAwait(false);
            await UpdateManifestAfterPublish(manifest, uploaded, filePath, ct).ConfigureAwait(false);

            return new SyncResult(SyncAction.Push, SyncOutcome.Published, force,
                RemoteVersionIdBefore: latest.VersionId,
                RemoteVersionIdAfter: uploaded.VersionId,
                LocalHashAfter: FileHash.Sha256OfFile(filePath));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SyncResult(SyncAction.Push, SyncOutcome.Failed, force, Message: ex.Message);
        }
    }

    /// <summary>
    /// Push by snapshotting the live database then publishing it. Same head precondition as PushFromFileAsync.
    /// </summary>
    public async Task<SyncResult> PushFromDatabaseAsync(string liveDbPath, string snapshotsDir, bool force = false, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(liveDbPath) || !File.Exists(liveDbPath))
            {
                var message = string.IsNullOrWhiteSpace(liveDbPath)
                    ? "Db path is empty."
                    : $"Db not found: {liveDbPath}";
                return new SyncResult(SyncAction.Push, SyncOutcome.Failed, force, Message: message);
            }
            var snapshotPath = SqliteBackup.CreateSnapshot(liveDbPath, snapshotsDir, snapshotFileName: null, verifyIntegrity: false);
            try
            {
                return await PushFromFileAsync(snapshotPath, force, ct).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(snapshotPath);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SyncResult(SyncAction.Push, SyncOutcome.Failed, force, Message: ex.Message);
        }
    }

    private async Task UpdateManifestAfterPublish(DbSyncManifest manifest, ObjectVersionInfo info, string filePath, CancellationToken ct)
    {
        manifest.Bucket = _settings.Bucket;
        manifest.Key = _settings.Key;
        manifest.VersionId = info.VersionId;
        manifest.Sha256 = info.VersionId; // policy: remote sha == versionId
        manifest.ETag = info.ETag;
        manifest.ContentLength = info.ContentLength;
        manifest.LastModifiedUtc = info.LastModifiedUtc;
        manifest.AppliedAtUtc = DateTimeOffset.UtcNow;
        manifest.LocalContentSha256 = FileHash.Sha256OfFile(filePath);

        await _manifestStore.SaveAsync(manifest, ct).ConfigureAwait(false);
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

    private static void TryDelete(string path)
    {
        try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); }
        catch { /* ignore */ }
    }
}

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
        string localDbBackupsDir,
        bool force = false,
        CancellationToken ct = default)
    {
        try
        {
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

            var localChanged = false;
            var manifest = await _manifestStore.LoadAsync(ct).ConfigureAwait(false);
            var localExists = File.Exists(localDbPath);
            if (localExists)
            {
                if (manifest?.ServerState == null || !string.Equals(FileHash.Sha256OfFile(localDbPath), manifest?.ServerState?.Sha256, StringComparison.Ordinal))
                    localChanged = true;
            }
            if (localChanged && !force)
                return new SyncResult(SyncAction.Pull, SyncOutcome.Conflict_LocalChanged, false,
                       Message: "Local content hash differs from last applied; use force to overwrite.");

            if (manifest?.ServerState?.VersionId == latest.VersionId)
                return new SyncResult(SyncAction.Pull, SyncOutcome.NoChange, force);

            if (localChanged)
            {
                var backupName = _backupNamer.GetName(new BackupNamingContext(
                    AppliedAtUtc: manifest?.AppliedAtUtc.UtcDateTime ?? File.GetCreationTimeUtc(localDbPath), 
                    RemoteVersionId: manifest?.ServerState?.VersionId, 
                    Reason: "pull-overwrite"));
                var backupPath = Path.Combine(localDbBackupsDir, backupName);
                File.Copy(localDbPath, backupPath, overwrite: true);
            }


            // Fetch to temp then atomic replace
            var tmp = localDbPath + $".{latest.VersionId}.tmp";
            try
            {
                await _s3.FetchObjectVersionToFileAsync(_settings.Bucket, _settings.Key, latest.VersionId, tmp, ct).ConfigureAwait(false);
                AtomicReplace(tmp, localDbPath);
            }
            finally { TryDelete(tmp); }

            var localHashAfter = FileHash.Sha256OfFile(localDbPath);

            // Update manifest
            await _manifestStore.SaveAsync(new DbSyncManifest(
                    ServerState: new DbSyncManifest.ServerStateInfo(
                            Bucket: _settings.Bucket,
                            Key: _settings.Key,
                            VersionId: latest.VersionId,
                            ETag: latest.ETag,
                            Sha256: localHashAfter,
                            ContentLength: latest.ContentLength,
                            LastModifiedUtc: latest.LastModifiedUtc
                        ),
                    AppliedAtUtc: DateTimeOffset.UtcNow
                ), ct).ConfigureAwait(false);

            return new SyncResult(SyncAction.Pull, localExists ? SyncOutcome.Replaced : SyncOutcome.Created, force);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SyncResult(SyncAction.Pull, SyncOutcome.Failed, force, Message: ex.Message);
        }
    }

    /// <summary>
    /// Push from a file. Enforces head precondition unless forced.
    /// </summary>
    public async Task<SyncResult> PushAsync(string localDbPath, string snapshotDir, bool force = false, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(localDbPath))
                return new SyncResult(SyncAction.Push, SyncOutcome.Failed, force, Message: "Local db not found.");

            var manifest = await _manifestStore.LoadAsync(ct).ConfigureAwait(false);
            var localHash = FileHash.Sha256OfFile(localDbPath);
            if (manifest?.ServerState != null && string.Equals(localHash, manifest.ServerState.Sha256, StringComparison.Ordinal))
                return new SyncResult(SyncAction.Push, SyncOutcome.NoChange, force);
            if (!force)
            {
                try
                {
                    var latest = await _s3.GetLatestVersionAsync(_settings.Bucket, _settings.Key, ct).ConfigureAwait(false);
                    if (manifest?.ServerState?.VersionId != latest.VersionId)
                        return new SyncResult(SyncAction.Push, SyncOutcome.Conflict_RemoteHeadMismatch, force,
                            Message: $"Remote head {latest.VersionId} != local {manifest?.ServerState?.VersionId}.");


                }
                catch (Exception ex) when (ex is DeletedObjectException or NoRemoteVersionException)
                {
                    // Allowed: pushing a fresh copy when remote is absent.
                }
            }

            var snapshotPath = SqliteBackup.CreateSnapshot(localDbPath, snapshotDir, verifyIntegrity: false);
            try
            {
                var uploaded = await _s3.UploadObjectAsync(_settings.Bucket, _settings.Key, snapshotPath, ct).ConfigureAwait(false);
                await _manifestStore.SaveAsync(new DbSyncManifest(
                                    ServerState: new DbSyncManifest.ServerStateInfo(
                                            Bucket: _settings.Bucket,
                                            Key: _settings.Key,
                                            VersionId: uploaded.VersionId,
                                            ETag: uploaded.ETag,
                                            Sha256: FileHash.Sha256OfFile(localDbPath),
                                            ContentLength: uploaded.ContentLength,
                                            LastModifiedUtc: uploaded.LastModifiedUtc
                                        ),
                                    AppliedAtUtc: DateTimeOffset.UtcNow
                                ), ct).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(snapshotPath);
            }
            return new SyncResult(SyncAction.Push, SyncOutcome.Published, force);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SyncResult(SyncAction.Push, SyncOutcome.Failed, force, Message: ex.Message);
        }
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

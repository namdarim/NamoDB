using Microsoft.Extensions.Options;
using Namo.App.Options;
using Namo.App.Models;
using Namo.Domain.DBSync;
using Namo.Infrastructure.DBSync;

namespace Namo.App.Services;

/// <summary>
/// App-level facade: hides sync enums/codes and returns a flat result for UI.
/// Also supplies deterministic backup names to the orchestrator.
/// </summary>
public sealed class DbSyncAppService
{
    private readonly SyncOrchestrator _sync;
    private readonly DbSyncPaths _paths;

    public DbSyncAppService(SyncOrchestrator sync, IOptions<DbSyncPaths> paths)
    {
        _sync = sync;
        _paths = paths.Value;
    }

    public async Task<DbSyncResult> PullAsync(bool force = false, CancellationToken ct = default)
    {
        var r = await _sync.PullAsync(_paths.LocalDbPath, BackupNamer, force, ct);
        return MapToAppResult(r);
    }

    public async Task<DbSyncResult> PushAsync(bool force = false, CancellationToken ct = default)
    {
        var r = await _sync.PushAsync(_paths.LocalDbPath, _paths.SnapshotDir, force, ct);
        return MapToAppResult(r);
    }


    private static DbSyncResult MapToAppResult(SyncResult r)
    {
        // Pull
        if (r.Action == SyncAction.Pull)
        {
            return r.Outcome switch
            {
                SyncOutcome.NoChange => new(true, "Already up to date."),
                SyncOutcome.Replaced => new(true, "Database updated from remote."),
                SyncOutcome.Conflict_LocalChanged => new(false, "Local changes detected. Retry with force to overwrite."),
                SyncOutcome.Conflict_RollbackRejected => new(false, "Remote version is older than local. Pull refused."),
                SyncOutcome.BackupAlreadyExists => new(false, "Backup path already exists. Choose a different name."),
                SyncOutcome.RemoteDeleted => new(false, "Remote object is deleted or missing."),
                SyncOutcome.Failed => new(false, r.Message ?? "Sync failed."),
                _ => new(false, r.Message ?? "Sync failed.")
            };
        }

        // Push
        return r.Outcome switch
        {
            SyncOutcome.Published => new(true, "Published successfully."),
            SyncOutcome.Conflict_RemoteHeadMismatch => new(false, "Remote head changed. Pull first or push with force."),
            SyncOutcome.RemoteDeleted => new(false, "Remote object is deleted or missing."),
            SyncOutcome.NoChange => new(true, "Nothing to publish."),
            SyncOutcome.Failed => new(false, r.Message ?? "Sync failed."),
            _ => new(false, r.Message ?? "Sync failed.")
        };
    }

    private string BackupNamer(BackupNamingContext ctx)
    {
        // Deterministic, self-explanatory backup filename.
        var dbName = Path.GetFileName(_paths.LocalDbPath);
        var name =
            $"{dbName}.bak.pull.{San(ctx.LocalVersionId)}__to__{San(ctx.RemoteVersionId)}." +
            $"{Prefix(ctx.LocalContentSha256, 8)}.{ctx.AppliedAtUtc:yyyyMMddTHHmmssZ}.{ctx.NowUtc:yyyyMMddTHHmmssZ}.db";

        var dir = Path.Combine(_paths.SnapshotDir, "backups");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);

        static string San(string s) => string.IsNullOrEmpty(s) ? "none" : s.Replace(':', '-').Replace('/', '-');
        static string Prefix(string s, int n) => string.IsNullOrEmpty(s) ? "none" : (s.Length <= n ? s : s[..n]);
    }
}

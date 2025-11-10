using Microsoft.Extensions.Options;
using Namo.App.Options;
using Namo.App.Models;
using Namo.Domain.DBSync;
using Namo.Infrastructure.DBSync;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Namo.App.Services
{
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
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _paths = (paths ?? throw new ArgumentNullException(nameof(paths))).Value
                     ?? throw new ArgumentNullException(nameof(paths.Value));
        }

        public async Task<DbSyncResult> PullAsync(bool force = false, CancellationToken ct = default)
        {
            var r = await _sync.PullAsync(_paths.LocalDbPath, _paths.LocalDbBackupsDir, force, ct).ConfigureAwait(false);
            return MapToAppResult(r);
        }

        public async Task<DbSyncResult> PushAsync(bool force = false, CancellationToken ct = default)
        {
            var r = await _sync.PushAsync(_paths.LocalDbPath, _paths.SnapshotDir, force, ct).ConfigureAwait(false);
            return MapToAppResult(r);
        }

        private static DbSyncResult MapToAppResult(SyncResult r)
        {
            // First handle cross-cutting outcomes
            switch (r.Outcome)
            {
                case SyncOutcome.RemoteDeleted:
                    return new(false, "Remote object is deleted or missing.");
                case SyncOutcome.Failed:
                    // Prefer orchestrator's message when available
                    return new(false, r.Message ?? "Sync failed.");
            }

            // Action-specific mapping
            if (r.Action == SyncAction.Pull)
            {
                return r.Outcome switch
                {
                    SyncOutcome.NoChange => new(true, "Already up to date."),
                    SyncOutcome.Created => new(true, "Database created from remote."),
                    SyncOutcome.Replaced => new(true, "Database updated from remote."),
                    SyncOutcome.Conflict_LocalChanged => new(false, "Local changes detected. Retry with force to overwrite."),
                    _ => new(false, $"Unhandled pull outcome: {r.Outcome}.") // safer than throwing
                };
            }

            // Push
            return r.Outcome switch
            {
                SyncOutcome.NoChange => new(true, "Nothing to publish."),
                SyncOutcome.Published => new(true, "Published successfully."),
                SyncOutcome.Conflict_RemoteHeadMismatch => new(false, "Remote head changed. Pull first or push with force."),
                _ => new(false, $"Unhandled push outcome: {r.Outcome}.")
            };
        }
    }
}

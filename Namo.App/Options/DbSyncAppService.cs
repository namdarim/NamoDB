using Microsoft.Extensions.Options;
using Namo.Infrastructure.DBSync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.App.Options;

public sealed class DbSyncAppService
{
    private readonly SyncOrchestrator _sync;
    private readonly DbSyncPathsOptions _paths;

    public DbSyncAppService(SyncOrchestrator sync, IOptions<DbSyncPathsOptions> paths)
    {
        _sync = sync;
        _paths = paths.Value;
    }

    public Task PullAsync(Func<CancellationToken, Task>? preReplaceHook = null, CancellationToken ct = default)
        => _sync.PullAsync(_paths.LocalDbPath, preReplaceHook, ct);

    public Task PushFromLiveDbAsync(IDictionary<string, string>? meta = null, CancellationToken ct = default)
        => _sync.PushFromLiveDbAsync(_paths.LocalDbPath, _paths.SnapshotDir, meta, ct);
}

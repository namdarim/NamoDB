using Microsoft.Extensions.DependencyInjection;
using Namo.Domain.Contracts.Cloud;
using Namo.Domain.Contracts.Sync;
using Namo.Infrastructure.Sync.Apply;
using Namo.Infrastructure.Sync.Snapshot;

namespace Namo.Infrastructure.Orchestrator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSnapshotSyncCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISqliteSnapshotCreator, SqliteVacuumSnapshotCreator>();
        services.AddSingleton<ISnapshotDownloader, VersionedSnapshotDownloader>();
        services.AddSingleton<ISqliteSnapshotApplier, SqliteSnapshotApplier>();
        services.AddSingleton<ISyncOrchestrator, VersionedSyncOrchestrator>();

        return services;
    }
}

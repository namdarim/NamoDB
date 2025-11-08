using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Namo.Domain;

namespace Namo.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseSyncCore(this IServiceCollection services, DatabaseSyncOrchestratorOptions? options = null)
    {
        services.TryAddSingleton<ISnapshotDownloadProvider, AtomicSnapshotDownloadProvider>();
        services.TryAddSingleton<ISqliteSnapshotFactory, SqliteSnapshotFactory>();
        services.TryAddSingleton<ILocalDatabaseApplier, SqliteLocalDatabaseApplier>();
        services.TryAddSingleton<IAppliedVersionStore>(sp => new JsonAppliedVersionStore(sp.GetRequiredService<IKeyValueStore>()));
        services.TryAddSingleton<IVersionedObjectClient>(sp => new S3VersionedObjectClient(sp.GetRequiredService<IAmazonS3>()));
        services.TryAddSingleton<IDatabaseSyncOrchestrator>(sp => new DatabaseSyncOrchestrator(
            sp.GetRequiredService<IVersionedObjectClient>(),
            sp.GetRequiredService<ISnapshotDownloadProvider>(),
            sp.GetRequiredService<ISqliteSnapshotFactory>(),
            sp.GetRequiredService<ILocalDatabaseApplier>(),
            sp.GetRequiredService<IAppliedVersionStore>(),
            sp.GetRequiredService<IAppPathProvider>(),
            options));

        return services;
    }
}

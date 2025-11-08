using Microsoft.Extensions.DependencyInjection;
using Namo.Domain.Contracts.Env;
using Namo.Domain.Contracts.Sync;
using Namo.Infrastructure.Cloud.S3;
using Namo.Infrastructure.Orchestrator;
using Namo.Infrastructure.Stores;

namespace Namo.WIN.Adapters;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsSnapshotSync(this IServiceCollection services, Action<S3ClientOptions> configureS3)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureS3);

        services.AddSingleton<IAppPaths>(_ => new WindowsAppPaths("NamoWinSync"));
        services.AddSingleton<IKeyValueStore>(provider =>
        {
            var paths = provider.GetRequiredService<IAppPaths>();
            return new FileKeyValueStore(paths.ResolvePath("kv"));
        });

        services.AddSingleton<IVersionInfoStore>(provider =>
        {
            var store = provider.GetRequiredService<IKeyValueStore>();
            return new JsonVersionInfoStore(store);
        });

        services.Configure<SyncOrchestratorOptions>(_ => { });
        services.AddVersionedS3Client(configureS3);
        services.AddSnapshotSyncCore();

        return services;
    }
}

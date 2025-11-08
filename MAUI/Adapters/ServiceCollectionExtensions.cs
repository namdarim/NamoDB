using Microsoft.Extensions.DependencyInjection;
using Namo.Domain.Contracts.Env;
using Namo.Domain.Contracts.Sync;
using Namo.Infrastructure.Cloud.S3;
using Namo.Infrastructure.Orchestrator;
using Namo.Infrastructure.Stores;

namespace Namo.MAUI.Adapters;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMauiSnapshotSync(this IServiceCollection services, Action<S3ClientOptions> configureS3)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureS3);

        services.AddSingleton<IKeyValueStore, InMemoryKeyValueStore>();
        services.AddSingleton<IAppPaths>(_ =>
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NamoMauiSync");
            return new RelativeAppPaths(basePath);
        });

        services.AddSingleton<IVersionInfoStore>(provider =>
        {
            var store = provider.GetRequiredService<IKeyValueStore>();
            return new JsonVersionInfoStore(store);
        });

        services.Configure<SyncOrchestratorOptions>(_ => { });
        services.AddVersionedS3Client();
        services.AddSnapshotSyncCore();

        return services;
    }
}

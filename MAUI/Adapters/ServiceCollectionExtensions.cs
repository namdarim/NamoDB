using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Namo.Domain.Contracts.Env;
using Namo.Domain.Contracts.Sync;
using Namo.Infrastructure.Cloud.S3;
using Namo.Infrastructure.Orchestrator;
using Namo.Infrastructure.Stores;

namespace Namo.MAUI.Adapters;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMauiSnapshotSync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IKeyValueStore, InMemoryKeyValueStore>();

        services.AddSingleton<IAppPaths>(_ =>
        {
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NamoMauiSync");
            return new RelativeAppPaths(basePath);
        });

        services.AddSingleton<IVersionInfoStore>(provider =>
        {
            var store = provider.GetRequiredService<IKeyValueStore>();
            return new JsonVersionInfoStore(store);
        });

        services.AddOptions<SyncOrchestratorOptions>()
            .Configure(_ => { });

        services.AddVersionedS3Client();
        services.AddSnapshotSyncCore();

        services.AddOptions<S3ClientOptions>()
            .BindConfiguration("S3Client")
            .ValidateOnStart();

        return services;
    }
}

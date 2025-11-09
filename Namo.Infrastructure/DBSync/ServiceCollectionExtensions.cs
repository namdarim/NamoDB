using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Namo.Infrastructure.DBSync;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register DbSync with a concrete KV store type and optional S3 settings factory.
    /// If s3SettingsFactory is null, S3Settings must be provided via IOptions&lt;S3Settings&gt;.
    /// </summary>
    public static IServiceCollection AddDbSync<TKeyValueStore>(
        this IServiceCollection services,
        Func<S3Settings>? s3SettingsFactory = null)
        where TKeyValueStore : class, Namo.Common.Abstractions.IKeyValueStore
    {
        // Provide S3Settings either from the factory or from IOptions<S3Settings>.
        services.AddSingleton(sp =>
        {
            if (s3SettingsFactory is not null)
            {
                var s = s3SettingsFactory();
                if (s is null) throw new InvalidOperationException("S3Settings factory returned null.");
                return s;
            }

            var fromOptions = sp.GetService<IOptions<S3Settings>>()?.Value;
            if (fromOptions is null) throw new InvalidOperationException(
                "S3Settings is not configured. Provide s3SettingsFactory or register IOptions<S3Settings>.");
            return fromOptions;
        });

        // Key-Value store implementation (must be DI-resolvable).
        services.AddSingleton<Namo.Common.Abstractions.IKeyValueStore, TKeyValueStore>();

        // Manifest repository
        services.AddSingleton(sp =>
            new DbSyncManifestStore(sp.GetRequiredService<Namo.Common.Abstractions.IKeyValueStore>(),
                                    logicalName: "dbsync.manifest"));

        // Orchestrator
        services.AddSingleton<S3StorageService>();
        services.AddSingleton<SyncOrchestrator>();

        return services;
    }

    /// <summary>
    /// Convenience overload: bind S3Settings from an IConfiguration section (e.g. "DbSync:S3").
    /// </summary>
    public static IServiceCollection AddDbSyncFromConfig<TKeyValueStore>(
        this IServiceCollection services,
        IConfiguration s3Section)
        where TKeyValueStore : class, Namo.Common.Abstractions.IKeyValueStore
    {
        if (s3Section is null) throw new ArgumentNullException(nameof(s3Section));

        services.AddOptions<S3Settings>().Bind(s3Section).ValidateOnStart();

        return services.AddDbSync<TKeyValueStore>(s3SettingsFactory: null);
    }
}

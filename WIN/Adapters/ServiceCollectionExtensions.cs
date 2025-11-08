using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Namo.Domain.Contracts.Env;
using Namo.Domain.Contracts.Sync;
using Namo.Domain.Sync;
using Namo.Infrastructure.Cloud.S3;
using Namo.Infrastructure.Orchestrator;
using Namo.Infrastructure.Stores;
using System.Linq;

namespace Namo.WIN.Adapters;

public static class ServiceCollectionExtensions
{
    private const string ConfigurationFileName = "appsettings.json";
    private const string S3ClientSectionName = "S3Client";

    public static IServiceCollection AddWindowsSnapshotSync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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

        ConfigureS3ClientOptions(services);
        services.Configure<SyncOrchestratorOptions>(_ => { });
        services.AddVersionedS3Client();
        services.AddSnapshotSyncCore();

        return services;
    }

    private static void ConfigureS3ClientOptions(IServiceCollection services)
    {
        var configuration = BuildConfiguration();
        var section = configuration.GetSection(S3ClientSectionName);

        if (!section.Exists())
        {
            throw new InvalidOperationException($"The '{S3ClientSectionName}' section is missing from '{ConfigurationFileName}'.");
        }

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IConfiguration)))
        {
            services.AddSingleton<IConfiguration>(configuration);
        }

        services.AddOptions<S3ClientOptions>()
            .Bind(section)
            .ValidateOnStart();
    }

    private static IConfiguration BuildConfiguration()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(ConfigurationFileName, optional: false, reloadOnChange: true);

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);
        }

        builder.AddEnvironmentVariables(prefix: "NAMO_");

        return builder.Build();
    }
}

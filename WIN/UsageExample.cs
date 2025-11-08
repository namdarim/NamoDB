using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Namo.Domain;
using Namo.Infrastructure;

namespace Namo.WinAdapters;

public static class UsageExample
{
    public static async Task RunAsync(string liveDbPath, string snapshotWorkingPath, string bucket, string key, CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAppPathProvider, InMemoryAppPathProvider>();
        services.AddSingleton<IKeyValueStore, InMemoryKeyValueStore>();
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new AmazonS3Config
        {
            ServiceURL = "https://s3.example.com",
            ForcePathStyle = true
        }));

        services.AddDatabaseSyncCore();

        await using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<IDatabaseSyncOrchestrator>();

        var published = await orchestrator.PublishAsync(liveDbPath, bucket, key, cancellationToken);
        var syncResult = await orchestrator.SyncAsync(bucket, key, liveDbPath, snapshotWorkingPath, cancellationToken);
    }
}

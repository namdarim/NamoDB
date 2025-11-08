using Microsoft.Extensions.DependencyInjection;
using Namo.Domain.Contracts.Cloud;
using Namo.Domain.Contracts.Sync;
using Namo.MAUI.Adapters;
using Namo.WIN.Adapters;

namespace Namo.Infrastructure.Orchestrator;

public static class UsageExamples
{
    public static async Task PublishAndSyncWithMauiAdaptersAsync(string liveDatabasePath, string snapshotTempPath, CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddMauiSnapshotSync(options =>
        {
            options.ServiceUrl = "https://s3-compatible.example";
            options.ForcePathStyle = true;
        });

        await RunPublishAndSyncAsync(services, liveDatabasePath, snapshotTempPath, cancellationToken).ConfigureAwait(false);
    }

    public static async Task PublishAndSyncWithWindowsAdaptersAsync(string liveDatabasePath, string snapshotTempPath, CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddWindowsSnapshotSync(options =>
        {
            options.ServiceUrl = "https://s3-compatible.example";
            options.ForcePathStyle = true;
        });

        await RunPublishAndSyncAsync(services, liveDatabasePath, snapshotTempPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunPublishAndSyncAsync(IServiceCollection services, string liveDatabasePath, string snapshotTempPath, CancellationToken cancellationToken)
    {
        services.AddSingleton<CloudObjectIdentifier>(_ => new CloudObjectIdentifier("my-bucket", "snapshots/database.db"));
        await using var provider = services.BuildServiceProvider();
        var orchestrator = provider.GetRequiredService<ISyncOrchestrator>();
        var identifier = provider.GetRequiredService<CloudObjectIdentifier>();

        await orchestrator.PublishAsync(liveDatabasePath, identifier, cancellationToken).ConfigureAwait(false);
        await orchestrator.SyncAsync(liveDatabasePath, snapshotTempPath, identifier, cancellationToken).ConfigureAwait(false);
    }
}

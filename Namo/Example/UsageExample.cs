using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Namo.S3;
using Namo.Sqlite;
using Namo.Storage;
using Namo.Sync;

namespace Namo.Example;

public static class UsageExample
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDirectory);

        var liveDatabasePath = Path.Combine(dataDirectory, "live.db");
        var snapshotCachePath = Path.Combine(dataDirectory, "snapshot.db");
        var previousBackupPath = liveDatabasePath + ".prev";
        var metadataDirectory = Path.Combine(dataDirectory, "kv");

        var keyValueStore = new FileKeyValueStore(metadataDirectory);
        var versionStore = new FileVersionMetadataStore(keyValueStore);
        var snapshotProvider = new SqliteVacuumSnapshotProvider(liveDatabasePath);
        var snapshotApplier = new SqliteBackupSnapshotApplier(liveDatabasePath, previousBackupPath);

        const string bucketName = "your-bucket";
        const string objectKey = "snapshots/live.db";

        using var s3Client = new AmazonS3Client(RegionEndpoint.USEast1);
        var cloudClient = new S3SnapshotCloudClient(s3Client, bucketName, objectKey, Path.GetDirectoryName(snapshotCachePath));

        var orchestrator = new DatabaseSyncOrchestrator(
            cloudClient,
            snapshotApplier,
            snapshotProvider,
            versionStore,
            snapshotCachePath);

        var publishResult = await orchestrator.PublishAsync("1.0.0", cancellationToken);
        Console.WriteLine($"Published VersionId: {publishResult.VersionId}");

        var applied = await orchestrator.SyncAsync(cancellationToken);
        Console.WriteLine(applied ? "Snapshot applied." : "Already up-to-date.");
    }
}

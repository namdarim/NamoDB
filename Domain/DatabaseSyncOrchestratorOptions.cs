namespace Namo.Domain;

public sealed record DatabaseSyncOrchestratorOptions(
    SqliteApplyOptions ApplyOptions,
    IReadOnlyDictionary<string, string>? AdditionalUploadMetadata,
    TimeSpan IntegrityRetryDelay,
    int IntegrityRetryCount)
{
    public static DatabaseSyncOrchestratorOptions Default { get; } = new(
        new SqliteApplyOptions(CheckpointWal: true, CreatePreviousBackup: true, BusyTimeout: TimeSpan.FromSeconds(5), BusyRetryCount: 3),
        AdditionalUploadMetadata: null,
        IntegrityRetryDelay: TimeSpan.FromSeconds(2),
        IntegrityRetryCount: 0);
}

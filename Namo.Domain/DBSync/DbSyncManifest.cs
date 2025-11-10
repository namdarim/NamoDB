namespace Namo.Domain.DBSync;

public sealed record DbSyncManifest(
    DbSyncManifest.ServerStateInfo? ServerState,
    DateTimeOffset AppliedAtUtc
)
{
    public sealed record ServerStateInfo(
        string Bucket,
        string Key,
        string VersionId,
        string ETag,
        string Sha256,
        long ContentLength,
        DateTimeOffset LastModifiedUtc
    );
}

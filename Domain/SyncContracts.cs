namespace Namo.Domain;

public sealed record PublishResult(VersionedObjectMetadata Metadata, string SnapshotPath);

public sealed record SyncResult(bool Changed, VersionedObjectMetadata? Metadata);

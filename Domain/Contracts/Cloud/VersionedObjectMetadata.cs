namespace Namo.Domain.Contracts.Cloud;

public sealed record VersionedObjectMetadata(
    string VersionId,
    string ETag,
    string Sha256,
    DateTimeOffset LastModifiedUtc,
    long ContentLength);

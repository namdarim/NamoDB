namespace Namo.Domain;

public sealed record VersionedObjectMetadata(
    string VersionId,
    string ETag,
    DateTimeOffset LastModifiedUtc,
    long Size,
    string? Sha256Hex);

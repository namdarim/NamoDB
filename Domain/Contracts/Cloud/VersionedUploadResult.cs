namespace Namo.Domain.Contracts.Cloud;

public sealed record VersionedUploadResult(
    CloudObjectIdentifier Identifier,
    VersionedObjectMetadata Metadata,
    DateTimeOffset CreatedAtUtc,
    string Sha256Hex);

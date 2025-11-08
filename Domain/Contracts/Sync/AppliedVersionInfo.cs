using Namo.Domain.Contracts.Cloud;

namespace Namo.Domain.Contracts.Sync;

public sealed record AppliedVersionInfo(
    CloudObjectIdentifier Identifier,
    VersionedObjectMetadata Metadata,
    string Sha256Hex,
    DateTimeOffset AppliedAtUtc);

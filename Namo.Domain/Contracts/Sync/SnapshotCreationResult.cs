namespace Namo.Domain.Contracts.Sync;

public sealed record SnapshotCreationResult(string SnapshotPath, long SizeBytes, string Sha256Hex);

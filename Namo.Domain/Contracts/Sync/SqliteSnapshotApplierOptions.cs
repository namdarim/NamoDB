namespace Namo.Domain.Contracts.Sync;

public sealed record SqliteSnapshotApplierOptions(bool CreateRollbackCopy, string? RollbackCopyPath = null);

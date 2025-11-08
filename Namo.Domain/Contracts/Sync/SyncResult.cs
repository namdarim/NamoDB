namespace Namo.Domain.Contracts.Sync;

public sealed record SyncResult(bool Updated, AppliedVersionInfo? AppliedVersion);

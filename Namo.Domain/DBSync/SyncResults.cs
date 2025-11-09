// Namo.Domain.DBSync/SyncResults.cs
namespace Namo.Domain.DBSync;

/// <summary>Operation kind.</summary>
public enum SyncAction
{
    Pull,
    Push
}

/// <summary>Unified status covering both Pull and Push scenarios.</summary>
public enum SyncOutcome
{
    NoChange,                       // nothing to do (already in sync)
    Replaced,                       // pull: local file replaced from remote
    Published,                      // push: new remote version published
    Conflict_LocalChanged,          // local content changed since last apply (hash mismatch)
    Conflict_RemoteHeadMismatch,    // remote head != local manifest head
    Conflict_RollbackRejected,      // discovered remote older than local policy allows
    RemoteDeleted,                  // remote object is deleted / has delete-marker / no versions
    BackupAlreadyExists,            // selected backup path already exists; no overwrite performed
    Failed                          // unexpected error mapped by orchestrator
}

/// <summary>Structured result for a sync attempt.</summary>
public sealed record SyncResult(
    SyncAction Action,
    SyncOutcome Outcome,
    bool Forced,
    string? Message = null,
    string? LocalBackupPath = null,
    string? LocalHashBefore = null,
    string? LocalHashAfter = null,
    string? LocalVersionIdBefore = null,
    string? RemoteVersionIdBefore = null,
    string? RemoteVersionIdAfter = null
);

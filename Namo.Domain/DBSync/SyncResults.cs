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
    Created,                       // pull: local file created from remote
    Replaced,                       // pull: local file replaced from remote
    Conflict_LocalChanged,          // local content changed since last apply (hash mismatch)
    Published,                      // push: new remote version published
    Conflict_RemoteHeadMismatch,    // remote head != local manifest head
    RemoteDeleted,                  // remote object is deleted / has delete-marker / no versions
    Failed                          // unexpected error mapped by orchestrator
}

/// <summary>Structured result for a sync attempt.</summary>
public sealed record SyncResult(
    SyncAction Action,
    SyncOutcome Outcome,
    bool Forced,
    string? Message = null
);

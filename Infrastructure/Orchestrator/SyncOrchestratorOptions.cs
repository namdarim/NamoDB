namespace Namo.Infrastructure.Orchestrator;

public sealed class SyncOrchestratorOptions
{
    public bool CreateRollbackCopy { get; set; }

    public string? RollbackCopyPath { get; set; }
}

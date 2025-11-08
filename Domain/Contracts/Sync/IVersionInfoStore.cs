namespace Namo.Domain.Contracts.Sync;

public interface IVersionInfoStore
{
    Task<AppliedVersionInfo?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(AppliedVersionInfo info, CancellationToken cancellationToken);
}

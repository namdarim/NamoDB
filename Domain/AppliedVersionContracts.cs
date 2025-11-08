namespace Namo.Domain;

public sealed record AppliedVersionInfo(
    string VersionId,
    string ETag,
    string Sha256Hex,
    long Size,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset CloudLastModifiedUtc);

public interface IAppliedVersionStore
{
    Task<AppliedVersionInfo?> GetAsync(string scope, CancellationToken cancellationToken);
    Task SetAsync(string scope, AppliedVersionInfo info, CancellationToken cancellationToken);
}

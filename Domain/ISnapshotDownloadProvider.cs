namespace Namo.Domain;

public interface ISnapshotDownloadProvider
{
    Task<string> DownloadAsync(Func<Stream, CancellationToken, Task> copyToStreamAsync, string destinationPath, string expectedSha256Hex, CancellationToken cancellationToken);
}

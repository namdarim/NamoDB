using System.Threading;
using System.Threading.Tasks;
using Namo.Models;

namespace Namo.S3;

public interface ISnapshotCloudClient
{
    Task<SnapshotVersionInfo?> GetLatestVersionAsync(CancellationToken cancellationToken);

    Task<SnapshotDownloadResult> DownloadVersionAsync(string versionId, CancellationToken cancellationToken);

    Task<SnapshotUploadResult> UploadSnapshotAsync(SnapshotUploadRequest request, CancellationToken cancellationToken);
}

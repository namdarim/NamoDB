using System;

namespace Namo.Models;

public sealed class SnapshotUploadResult
{
    public SnapshotUploadResult(string versionId, string eTag)
    {
        VersionId = versionId ?? throw new ArgumentNullException(nameof(versionId));
        ETag = eTag ?? throw new ArgumentNullException(nameof(eTag));
    }

    public string VersionId { get; }

    public string ETag { get; }
}

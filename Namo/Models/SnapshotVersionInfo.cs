using System;

namespace Namo.Models;

public sealed class SnapshotVersionInfo
{
    public SnapshotVersionInfo(string versionId, string eTag, long contentLength, DateTimeOffset lastModified, string? sha256)
    {
        VersionId = versionId ?? throw new ArgumentNullException(nameof(versionId));
        ETag = eTag ?? throw new ArgumentNullException(nameof(eTag));
        ContentLength = contentLength;
        LastModified = lastModified;
        Sha256 = sha256;
    }

    public string VersionId { get; }

    public string ETag { get; }

    public long ContentLength { get; }

    public DateTimeOffset LastModified { get; }

    public string? Sha256 { get; }
}

using System;
using System.Text.Json.Serialization;

namespace Namo.Models;

public sealed class AppliedVersionInfo
{
    [JsonConstructor]
    public AppliedVersionInfo(string versionId, string eTag, string sha256, long bytes, DateTimeOffset appliedAtUtc)
    {
        VersionId = versionId ?? throw new ArgumentNullException(nameof(versionId));
        ETag = eTag ?? throw new ArgumentNullException(nameof(eTag));
        Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
        Bytes = bytes;
        AppliedAtUtc = appliedAtUtc;
    }

    public string VersionId { get; }

    public string ETag { get; }

    public string Sha256 { get; }

    public long Bytes { get; }

    public DateTimeOffset AppliedAtUtc { get; }
}

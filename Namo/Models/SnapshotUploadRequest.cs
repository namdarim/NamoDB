using System;
using System.Collections.Generic;

namespace Namo.Models;

public sealed class SnapshotUploadRequest
{
    public SnapshotUploadRequest(string snapshotFilePath, IReadOnlyDictionary<string, string> metadata)
    {
        if (string.IsNullOrWhiteSpace(snapshotFilePath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(snapshotFilePath));
        }

        SnapshotFilePath = snapshotFilePath;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public string SnapshotFilePath { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}

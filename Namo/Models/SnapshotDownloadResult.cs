using System;
using System.IO;
using System.Threading.Tasks;

namespace Namo.Models;

public sealed class SnapshotDownloadResult : IDisposable, IAsyncDisposable
{
    public SnapshotDownloadResult(SnapshotVersionInfo version, string tempFilePath)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        TempFilePath = tempFilePath ?? throw new ArgumentNullException(nameof(tempFilePath));
    }

    public SnapshotVersionInfo Version { get; }

    public string TempFilePath { get; }

    public void Dispose()
    {
        try
        {
            if (File.Exists(TempFilePath))
            {
                File.Delete(TempFilePath);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

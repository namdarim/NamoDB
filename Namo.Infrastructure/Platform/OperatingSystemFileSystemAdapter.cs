using System.IO;
using System.Threading;
using Namo.Domain.Contracts.Env;

namespace Namo.Infrastructure.Platform;

public sealed class OperatingSystemFileSystemAdapter : IFileSystemAdapter
{
    private const int DefaultBufferSize = 81920;

    public string CreateTemporaryFilePath(string prefix, string extension)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ArgumentException.ThrowIfNullOrEmpty(extension);

        var tempDirectory = Path.GetTempPath();
        var fileName = $"{prefix}-{Guid.NewGuid():N}{extension}";
        return Path.Combine(tempDirectory, fileName);
    }

    public ValueTask<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize, useAsync: true);
        return ValueTask.FromResult<Stream>(stream);
    }

    public ValueTask DeleteFileIfExistsAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValueTask.CompletedTask;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return ValueTask.CompletedTask;
    }
}

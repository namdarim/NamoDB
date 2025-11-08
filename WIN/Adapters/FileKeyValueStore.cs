using Namo.Domain.Contracts.Env;

namespace Namo.WIN.Adapters;

public sealed class FileKeyValueStore : IKeyValueStore
{
    private readonly string _rootDirectory;

    public FileKeyValueStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var buffer = new byte[stream.Length];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        if (totalRead != buffer.Length)
        {
            Array.Resize(ref buffer, totalRead);
        }

        return buffer;
    }

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        var tempPath = path + ".tmp";
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await stream.WriteAsync(value, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var safeName = Uri.EscapeDataString(key);
        return Path.Combine(_rootDirectory, safeName);
    }
}

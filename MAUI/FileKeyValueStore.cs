using System.Security.Cryptography;
using System.Text;
using Namo.Domain;

namespace Namo.MAUIAdapters;

public sealed class FileKeyValueStore : IKeyValueStore
{
    private readonly string _rootDirectory;

    public FileKeyValueStore(IAppPathProvider pathProvider)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        _rootDirectory = Path.Combine(pathProvider.GetDataDirectory(), "kv");
        Directory.CreateDirectory(_rootDirectory);
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

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
    }

    public async Task WriteAtomicAsync(string key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        var directory = Path.GetDirectoryName(path) ?? _rootDirectory;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tmpPath = path + ".tmp" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tmpPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }

            throw;
        }
    }

    private string ResolvePath(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        var fileName = Convert.ToHexString(hash);
        var directory = Path.Combine(_rootDirectory, fileName[..2]);
        return Path.Combine(directory, fileName);
    }
}

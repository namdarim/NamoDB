using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Namo.Storage;

public sealed class FileKeyValueStore : IKeyValueStore
{
    private readonly string _directoryPath;

    public FileKeyValueStore(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(directoryPath));
        }

        _directoryPath = directoryPath;
        Directory.CreateDirectory(_directoryPath);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(key);
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(key);
        var tempFilePath = filePath + ".tmp";

        await using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteAsync(value.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempFilePath, filePath, true);
    }

    private string GetFilePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(key));
        }

        foreach (var character in Path.GetInvalidFileNameChars())
        {
            key = key.Replace(character, '_');
        }

        return Path.Combine(_directoryPath, key + ".json");
    }
}

using System.Collections.Concurrent;
using Namo.Domain;

namespace Namo.WinAdapters;

public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(key, out var data))
        {
            return Task.FromResult<Stream?>(new MemoryStream(data, writable: false));
        }

        return Task.FromResult<Stream?>(null);
    }

    public Task WriteAtomicAsync(string key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        _store[key] = data.ToArray();
        return Task.CompletedTask;
    }
}

using System.Collections.Concurrent;
using Namo.Domain.Contracts.Env;

namespace Namo.MAUI.Adapters;

public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _store.TryGetValue(key, out var value);
        return Task.FromResult<byte[]?>(value);
    }

    public Task WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _store[key] = value.ToArray();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

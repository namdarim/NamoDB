using System.Threading;

namespace Namo.Common.Abstractions;

// Minimal async key-value store abstraction for manifest persistence.
public interface IKeyValueStore
{
    Task<string?> GetStringAsync(string key, CancellationToken ct = default);
    Task SetStringAsync(string key, string value, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

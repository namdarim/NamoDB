using Namo.Common.Abstractions;

namespace Namo.MAUI.Storage;

public sealed class MauiPreferencesKeyValueStore : IKeyValueStore
{
    private readonly string _ns;

    public MauiPreferencesKeyValueStore(string? nsPrefix = null)
    {
        _ns = string.IsNullOrWhiteSpace(nsPrefix) ? "Namo.Preferences." : nsPrefix;
    }

    private string K(string key) => _ns + key;

    public Task<string?> GetStringAsync(string key, CancellationToken ct = default)
        => Task.FromResult(Preferences.Get(K(key), (string?)null));

    public Task SetStringAsync(string key, string value, CancellationToken ct = default)
    {
        Preferences.Set(K(key), value);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        Preferences.Remove(K(key));
        return Task.CompletedTask;
    }
}

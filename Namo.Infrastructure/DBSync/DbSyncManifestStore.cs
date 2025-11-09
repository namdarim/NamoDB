using System.Text.Json;
using Namo.Common.Abstractions;
using Namo.Domain.DBSync;

namespace Namo.Infrastructure.DBSync;

// Thin repository for persisting DbSyncManifest via IKeyValueStore.
public sealed class DbSyncManifestStore
{
    private readonly IKeyValueStore _kv;
    private readonly string _key;

    public DbSyncManifestStore(IKeyValueStore kv, string logicalName = "dbsync.manifest")
    {
        _kv = kv ?? throw new ArgumentNullException(nameof(kv));
        _key = logicalName;
    }

    public async Task<DbSyncManifest?> LoadAsync(CancellationToken ct = default)
    {
        var json = await _kv.GetStringAsync(_key, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<DbSyncManifest>(json);
    }

    public Task SaveAsync(DbSyncManifest manifest, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(manifest);
        return _kv.SetStringAsync(_key, json, ct);
    }

    public Task ClearAsync(CancellationToken ct = default) => _kv.RemoveAsync(_key, ct);
}

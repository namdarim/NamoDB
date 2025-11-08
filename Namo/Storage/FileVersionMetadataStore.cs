using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Namo.Models;

namespace Namo.Storage;

public sealed class FileVersionMetadataStore : IVersionMetadataStore
{
    private const string VersionKey = "sqlite.snapshot.version";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IKeyValueStore _innerStore;

    public FileVersionMetadataStore(IKeyValueStore innerStore)
    {
        _innerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
    }

    public async Task<AppliedVersionInfo?> GetAsync(CancellationToken cancellationToken)
    {
        var data = await _innerStore.GetAsync(VersionKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AppliedVersionInfo>(data, JsonOptions);
    }

    public async Task SetAsync(AppliedVersionInfo version, CancellationToken cancellationToken)
    {
        if (version is null)
        {
            throw new ArgumentNullException(nameof(version));
        }

        var payload = JsonSerializer.Serialize(version, JsonOptions);
        await _innerStore.SetAsync(VersionKey, payload, cancellationToken).ConfigureAwait(false);
    }
}

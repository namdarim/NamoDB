using System.Text.Json;
using Namo.Domain;

namespace Namo.Infrastructure;

public sealed class JsonAppliedVersionStore : IAppliedVersionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IKeyValueStore _store;

    public JsonAppliedVersionStore(IKeyValueStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<AppliedVersionInfo?> GetAsync(string scope, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);
        await using var stream = await _store.OpenReadAsync(scope, cancellationToken).ConfigureAwait(false);
        if (stream == null)
        {
            return null;
        }

        var dto = await JsonSerializer.DeserializeAsync<AppliedVersionDto>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (dto == null)
        {
            return null;
        }

        return new AppliedVersionInfo(
            dto.VersionId,
            dto.ETag,
            dto.Sha256Hex,
            dto.Size,
            dto.AppliedAtUtc,
            dto.CloudLastModifiedUtc);
    }

    public async Task SetAsync(string scope, AppliedVersionInfo info, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentNullException.ThrowIfNull(info);

        var dto = new AppliedVersionDto
        {
            VersionId = info.VersionId,
            ETag = info.ETag,
            Sha256Hex = info.Sha256Hex,
            Size = info.Size,
            AppliedAtUtc = info.AppliedAtUtc,
            CloudLastModifiedUtc = info.CloudLastModifiedUtc
        };

        using var buffer = new MemoryStream();
        await JsonSerializer.SerializeAsync(buffer, dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await _store.WriteAtomicAsync(scope, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private sealed class AppliedVersionDto
    {
        public required string VersionId { get; init; }
        public required string ETag { get; init; }
        public required string Sha256Hex { get; init; }
        public required long Size { get; init; }
        public required DateTimeOffset AppliedAtUtc { get; init; }
        public required DateTimeOffset CloudLastModifiedUtc { get; init; }
    }
}

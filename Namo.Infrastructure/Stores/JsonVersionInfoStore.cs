using System.Text.Json;
using Namo.Domain.Contracts.Cloud;
using Namo.Domain.Contracts.Env;
using Namo.Domain.Contracts.Sync;

namespace Namo.Infrastructure.Stores;

public sealed class JsonVersionInfoStore : IVersionInfoStore
{
    private readonly IKeyValueStore _store;
    private readonly string _key;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public JsonVersionInfoStore(IKeyValueStore store, string key = "snapshot-version")
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _key = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Key is required.", nameof(key)) : key;
    }

    public async Task<AppliedVersionInfo?> ReadAsync(CancellationToken cancellationToken)
    {
        var payload = await _store.ReadAsync(_key, cancellationToken).ConfigureAwait(false);
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        var dto = JsonSerializer.Deserialize<AppliedVersionDto>(payload, _serializerOptions);
        if (dto is null)
        {
            return null;
        }

        var identifier = new CloudObjectIdentifier(dto.Bucket, dto.Key);
        var metadata = new VersionedObjectMetadata(dto.VersionId, dto.ETag, dto.Sha256, dto.LastModifiedUtc, dto.ContentLength);
        return new AppliedVersionInfo(identifier, metadata, dto.Sha256, dto.AppliedAtUtc);
    }

    public Task WriteAsync(AppliedVersionInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);

        var dto = new AppliedVersionDto
        {
            Bucket = info.Identifier.BucketName,
            Key = info.Identifier.ObjectKey,
            VersionId = info.Metadata.VersionId,
            ETag = info.Metadata.ETag,
            Sha256 = info.Metadata.Sha256,
            ContentLength = info.Metadata.ContentLength,
            LastModifiedUtc = info.Metadata.LastModifiedUtc!.Value,
            AppliedAtUtc = info.AppliedAtUtc
        };

        var payload = JsonSerializer.SerializeToUtf8Bytes(dto, _serializerOptions);
        return _store.WriteAsync(_key, payload, cancellationToken);
    }

    private sealed class AppliedVersionDto
    {
        public string Bucket { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string VersionId { get; set; } = string.Empty;

        public string ETag { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public long ContentLength { get; set; }

        public DateTimeOffset LastModifiedUtc { get; set; }

        public DateTimeOffset AppliedAtUtc { get; set; }
    }
}

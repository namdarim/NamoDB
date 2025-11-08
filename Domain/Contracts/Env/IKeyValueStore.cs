namespace Namo.Domain.Contracts.Env;

public interface IKeyValueStore
{
    Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken);

    Task WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

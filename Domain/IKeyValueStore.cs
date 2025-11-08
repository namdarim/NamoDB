namespace Namo.Domain;

public interface IKeyValueStore
{
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task WriteAtomicAsync(string key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

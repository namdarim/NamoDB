using System.Threading;
using System.Threading.Tasks;

namespace Namo.Storage;

public interface IKeyValueStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, CancellationToken cancellationToken);
}

using System.Threading;
using System.Threading.Tasks;
using Namo.Models;

namespace Namo.Storage;

public interface IVersionMetadataStore
{
    Task<AppliedVersionInfo?> GetAsync(CancellationToken cancellationToken);

    Task SetAsync(AppliedVersionInfo version, CancellationToken cancellationToken);
}

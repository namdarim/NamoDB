using System.Threading;
using System.Threading.Tasks;

namespace Namo.Sqlite;

public interface ISqliteSnapshotApplier
{
    Task ApplySnapshotAsync(string snapshotFilePath, CancellationToken cancellationToken);
}

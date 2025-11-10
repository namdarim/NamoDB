using System.Threading;
using System.Threading.Tasks;

namespace Namo.Sqlite;

public interface ISqliteSnapshotProvider
{
    Task CreateSnapshotAsync(string snapshotFilePath, CancellationToken cancellationToken);
}

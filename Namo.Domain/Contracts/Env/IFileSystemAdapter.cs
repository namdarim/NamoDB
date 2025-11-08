using System.IO;
using System.Threading;

namespace Namo.Domain.Contracts.Env;

public interface IFileSystemAdapter
{
    string CreateTemporaryFilePath(string prefix, string extension);

    ValueTask<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);

    ValueTask DeleteFileIfExistsAsync(string path, CancellationToken cancellationToken);
}

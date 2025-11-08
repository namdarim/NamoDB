using Namo.Domain.Contracts.Env;

namespace Namo.MAUI.Adapters;

public sealed class RelativeAppPaths : IAppPaths
{
    public RelativeAppPaths(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseDirectory);
        BaseDirectory = baseDirectory;
        Directory.CreateDirectory(BaseDirectory);
    }

    public string BaseDirectory { get; }

    public string ResolvePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        var path = Path.Combine(BaseDirectory, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return path;
    }
}

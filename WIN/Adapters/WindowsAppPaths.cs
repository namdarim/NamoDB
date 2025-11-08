using Namo.Domain.Contracts.Env;

namespace Namo.WIN.Adapters;

public sealed class WindowsAppPaths : IAppPaths
{
    public WindowsAppPaths(string applicationName)
    {
        ArgumentException.ThrowIfNullOrEmpty(applicationName);
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), applicationName);
        Directory.CreateDirectory(root);
        BaseDirectory = root;
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

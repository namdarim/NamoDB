namespace Namo.Domain.Contracts.Env;

public interface IAppPaths
{
    string BaseDirectory { get; }

    string ResolvePath(string relativePath);
}

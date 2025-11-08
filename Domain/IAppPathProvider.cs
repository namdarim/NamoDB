namespace Namo.Domain;

public interface IAppPathProvider
{
    string GetTemporaryDirectory();
    string GetDataDirectory();
}

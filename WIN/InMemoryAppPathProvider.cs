using Namo.Domain;

namespace Namo.WinAdapters;

public sealed class InMemoryAppPathProvider : IAppPathProvider
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "NamoWinTemp");
    private readonly string _dataDirectory = Path.Combine(AppContext.BaseDirectory, "win-data");

    public InMemoryAppPathProvider()
    {
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_dataDirectory);
    }

    public string GetDataDirectory() => _dataDirectory;

    public string GetTemporaryDirectory() => _tempDirectory;
}

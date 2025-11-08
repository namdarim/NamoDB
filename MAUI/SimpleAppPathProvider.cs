using Namo.Domain;

namespace Namo.MAUIAdapters;

public sealed class SimpleAppPathProvider : IAppPathProvider
{
    private readonly string _dataDirectory;
    private readonly string _tempDirectory;

    public SimpleAppPathProvider(string? dataDirectory = null, string? tempDirectory = null)
    {
        _dataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");
        _tempDirectory = tempDirectory ?? Path.Combine(Path.GetTempPath(), "NamoDbSnapshots");
        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_tempDirectory);
    }

    public string GetDataDirectory() => _dataDirectory;

    public string GetTemporaryDirectory() => _tempDirectory;
}

using System.Text.Json;
using Namo.Common.Abstractions;

namespace Namo.WIN.Storage;

public sealed class WinFileKeyValueStore : IKeyValueStore
{
    private readonly string _path;
    private static readonly object _gate = new();

    private sealed class Db { public Dictionary<string, string> S { get; set; } = new(StringComparer.Ordinal); }

    public WinFileKeyValueStore(string manifestJsonPath)
    {
        if (string.IsNullOrWhiteSpace(manifestJsonPath)) throw new ArgumentNullException(nameof(manifestJsonPath));
        _path = manifestJsonPath;
    }

    public Task<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var db = Load();
            return Task.FromResult(db.S.TryGetValue(key, out var v) ? v : null);
        }
    }

    public Task SetStringAsync(string key, string value, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var db = Load();
            db.S[key] = value;
            Save(db);
            return Task.CompletedTask;
        }
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var db = Load();
            if (db.S.Remove(key)) Save(db);
            return Task.CompletedTask;
        }
    }

    private Db Load()
    {
        if (!File.Exists(_path)) return new Db();
        using var fs = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonSerializer.Deserialize<Db>(fs) ?? new Db();
    }

    private void Save(Db db)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        using (var fs = File.Create(tmp)) { JsonSerializer.Serialize(fs, db); fs.Flush(true); }

        if (OperatingSystem.IsWindows() && File.Exists(_path))
        {
            var bak = _path + ".bak";
            try { File.Replace(tmp, _path, bak, ignoreMetadataErrors: true); }
            finally { if (File.Exists(bak)) File.Delete(bak); }
        }
        else
        {
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }
    }
}

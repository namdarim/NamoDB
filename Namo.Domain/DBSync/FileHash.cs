using System.Security.Cryptography;

namespace Namo.Infrastructure.DBSync;

public static class FileHash
{
    public static string Sha256OfFile(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash); // uppercase hex
    }
}

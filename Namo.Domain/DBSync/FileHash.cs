using System.Security.Cryptography;

namespace Namo.Infrastructure.DBSync;

public static class FileHash
{
    public static string Sha256OfFile(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
//        using var fs = new FileStream(
//    path,
//    FileMode.Open,
//    FileAccess.Read,
//    FileShare.ReadWrite // widest practical share on Windows
//);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash); // uppercase hex
    }
}

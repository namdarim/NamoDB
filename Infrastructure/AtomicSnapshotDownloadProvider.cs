using System.Security.Cryptography;
using Namo.Domain;

namespace Namo.Infrastructure;

public sealed class AtomicSnapshotDownloadProvider : ISnapshotDownloadProvider
{
    public async Task<string> DownloadAsync(Func<Stream, CancellationToken, Task> copyToStreamAsync, string destinationPath, string expectedSha256Hex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(copyToStreamAsync);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);
        ArgumentException.ThrowIfNullOrEmpty(expectedSha256Hex);

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var tmpPath = destinationPath + ".tmp";
        if (File.Exists(tmpPath))
        {
            File.Delete(tmpPath);
        }

        try
        {
            await using (var fileStream = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await copyToStreamAsync(fileStream, cancellationToken).ConfigureAwait(false);
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var actualHash = await ComputeSha256Async(tmpPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualHash, expectedSha256Hex.ToUpperInvariant(), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Checksum mismatch. Expected {expectedSha256Hex}, computed {actualHash}.");
            }

            File.Move(tmpPath, destinationPath, overwrite: true);
            return destinationPath;
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }

            throw;
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}

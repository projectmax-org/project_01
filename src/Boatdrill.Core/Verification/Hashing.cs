using System.Security.Cryptography;

namespace Boatdrill.Core.Verification;

internal static class Hashing
{
    public static async Task<string> Sha256HexAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        return Convert.ToHexStringLower(await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false));
    }
}

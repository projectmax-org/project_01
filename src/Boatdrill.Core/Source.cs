using System.Security.Cryptography;
using System.Text;

namespace Boatdrill.Core;

/// <summary>A folder whose files must be restorable: the thing a drill protects.</summary>
public sealed record Source(string Id, string Name, string RootPath)
{
    /// <summary>
    /// Patterns left out of every drill, matched case-insensitively against each path segment and the
    /// file name: "node_modules", ".git", "*.tmp", "Thumbs.db".
    /// </summary>
    public IReadOnlyList<string> Excludes { get; init; } = [];

    public static Source FromPath(string rootPath, string? name = null)
    {
        var full = Path.GetFullPath(rootPath);
        return new Source(IdForPath(full), name ?? DisplayName(full), full);
    }

    /// <summary>A short id that is the same for the same path on every run, so history lines up.</summary>
    public static string IdForPath(string path)
    {
        var normalised = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (OperatingSystem.IsWindows()) normalised = normalised.ToUpperInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)))[..12];
    }

    /// <summary>The last path segment, or the whole path for a volume root.</summary>
    internal static string DisplayName(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }
}

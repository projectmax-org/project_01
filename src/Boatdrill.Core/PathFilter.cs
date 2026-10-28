using System.Text.RegularExpressions;

namespace Boatdrill.Core;

/// <summary>Case-insensitive wildcard matching of exclude patterns against the segments of a relative path.</summary>
internal sealed class PathFilter
{
    private static readonly char[] Separators = ['/', '\\'];
    private readonly Regex[] _patterns;

    public PathFilter(IEnumerable<string> patterns)
    {
        _patterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new Regex(
                "^" + Regex.Escape(p.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .ToArray();
    }

    /// <summary>True when any segment of the path (folders or the file name) matches any pattern.</summary>
    public bool Excludes(string relativePath)
    {
        if (_patterns.Length == 0) return false;
        foreach (var segment in relativePath.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var pattern in _patterns)
            {
                if (pattern.IsMatch(segment)) return true;
            }
        }

        return false;
    }
}

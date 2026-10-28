using System.Text.Json;
using Boatdrill.Core.Drill;

namespace Boatdrill.Core.History;

/// <summary>
/// Append-only drill history, one JSON report per line. Trends, reachability and (from v0.2) the hash
/// ledger are read from it. A half-written last line, left by a crash or a power cut, is skipped, not fatal.
/// </summary>
public sealed class HistoryStore(string filePath)
{
    public string FilePath { get; } = filePath;

    public async Task AppendAsync(DrillReport report, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var line = JsonSerializer.Serialize(report, BoatdrillJson.Options) + "\n";
        await File.AppendAllTextAsync(FilePath, line, ct).ConfigureAwait(false);
    }

    public IReadOnlyList<DrillReport> ReadAll()
    {
        if (!File.Exists(FilePath)) return [];

        var reports = new List<DrillReport>();
        foreach (var line in File.ReadLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var report = JsonSerializer.Deserialize<DrillReport>(line, BoatdrillJson.Options);
                if (report is not null) reports.Add(report);
            }
            catch (JsonException)
            {
                // A torn line; see the class summary.
            }
        }

        return reports;
    }

    public DrillReport? Latest() => ReadAll().LastOrDefault();

    /// <summary>Share of the last <paramref name="window"/> drills in which the destination was reachable, or null without history.</summary>
    public double? Reachability(string destinationId, int window = 10)
    {
        var seen = ReadAll()
            .SelectMany(r => r.Destinations)
            .Where(d => d.DestinationId == destinationId)
            .TakeLast(window)
            .ToList();
        return seen.Count == 0 ? null : (double)seen.Count(d => d.Reachable) / seen.Count;
    }
}

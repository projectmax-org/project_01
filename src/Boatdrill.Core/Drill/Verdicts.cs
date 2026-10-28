using System.Text;
using Boatdrill.Core.Scoring;
using Boatdrill.Core.Text;

namespace Boatdrill.Core.Drill;

/// <summary>Turns a result into the one plain sentence the app shows under the score.</summary>
public static class Verdicts
{
    public static string Sentence(DestinationResult r, DateTimeOffset now)
    {
        var name = r.DestinationName;
        switch (r.Score.Verdict)
        {
            case Verdict.Unreachable:
                return $"{name} could not be reached: {r.ProbeDetail}. Plug it in or check the share, then run a drill.";
            case Verdict.NothingToCheck:
                return $"{name} is reachable, but no files were sampled, so there is nothing to verify yet.";
        }

        var lead = r.Score.Verdict switch
        {
            Verdict.WouldRestore => $"{name} would restore.",
            Verdict.WouldMostlyRestore => $"{name} would mostly restore.",
            Verdict.NeedsAttention => $"{name} needs attention.",
            _ => $"{name} would not restore.",
        };

        var facts = new List<string> { $"{r.Verified:N0} of {r.Sampled:N0} sampled files read back correctly" };
        if (r.Missing > 0) facts.Add($"{r.Missing:N0} {(r.Missing == 1 ? "has" : "have")} no copy");
        if (r.Outdated > 0) facts.Add($"{r.Outdated:N0} {(r.Outdated == 1 ? "is" : "are")} older than the original");
        if (r.Corrupt > 0) facts.Add($"{r.Corrupt:N0} differ{(r.Corrupt == 1 ? "s" : "")} from the original without being older, which points to corruption");
        if (r.Placeholder > 0) facts.Add($"{r.Placeholder:N0} exist{(r.Placeholder == 1 ? "s" : "")} only in the cloud, not on this disk");
        if (r.Errors > 0) facts.Add($"{r.Errors:N0} could not be read");

        var text = new StringBuilder(lead).Append(' ').Append(Join(facts)).Append('.');

        var lost = r.Bindings.Where(b => b.ProbeTried > 0 && b.ProbeHits == 0).Select(b => b.SourceName).ToList();
        if (lost.Count > 0)
        {
            text.Append($" No copy of {Join(lost)} was found under {r.RootPath}.");
        }

        if (r.BehindSince is { } since)
        {
            text.Append($" The oldest change not yet copied here is from {since.ToLocalTime():d MMMM yyyy} ({Humanize.Age(now - since)} ago).");
        }

        if (r.EstimatedFullRestore is { } estimate && r.PopulationBytes > 0)
        {
            text.Append($" A full restore of {Humanize.Bytes(r.PopulationBytes)} would take about {Humanize.Duration(estimate)} at the speed measured.");
        }

        return text.ToString();
    }

    private static string Join(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => string.Empty,
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
    };
}

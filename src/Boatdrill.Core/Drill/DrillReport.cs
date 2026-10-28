using Boatdrill.Core.Scoring;

namespace Boatdrill.Core.Drill;

public sealed record SourceSummary(
    string SourceId,
    string Name,
    string RootPath,
    long Population,
    long PopulationBytes,
    int Sampled,
    long SampledBytes,
    long SkippedLargeFiles,
    long SkippedChurningFiles,
    DateTimeOffset? NewestWriteUtc);

/// <summary>One drill, start to finish. Appended to the history as one line of JSON.</summary>
public sealed record DrillReport
{
    public required string Id { get; init; }

    public required DateTimeOffset StartedUtc { get; init; }

    public required DateTimeOffset FinishedUtc { get; init; }

    public required DrillTrigger Trigger { get; init; }

    public required IReadOnlyList<SourceSummary> Sources { get; init; }

    public required IReadOnlyList<DestinationResult> Destinations { get; init; }

    public TimeSpan Duration => FinishedUtc - StartedUtc;

    /// <summary>The worst destination's verdict, which is what the tray icon shows.</summary>
    public Verdict Worst => Destinations.Count == 0 ? Verdict.NothingToCheck : Destinations.Max(d => d.Score.Verdict);
}

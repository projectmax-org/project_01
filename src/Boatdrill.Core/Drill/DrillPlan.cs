using Boatdrill.Core.Sampling;

namespace Boatdrill.Core.Drill;

public enum DrillTrigger
{
    Manual,
    Scheduled,
}

/// <summary>Everything one drill needs: what to protect, where copies should be, how much to read.</summary>
public sealed record DrillPlan
{
    public required IReadOnlyList<Source> Sources { get; init; }

    public required IReadOnlyList<Destination> Destinations { get; init; }

    /// <summary>Folder on this PC that sampled copies are restored into. Emptied again when the drill ends.</summary>
    public required string ScratchDirectory { get; init; }

    public SampleOptions Sampling { get; init; } = new();

    public DrillTrigger Trigger { get; init; } = DrillTrigger.Manual;
}

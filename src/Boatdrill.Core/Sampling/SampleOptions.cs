namespace Boatdrill.Core.Sampling;

public sealed record SampleOptions
{
    /// <summary>Files per source. A few hundred gives a margin of error of a few per cent; see docs/SCORING.md.</summary>
    public int SampleSize { get; init; } = 200;

    /// <summary>Most bytes a drill reads back per source, so a drill stays minutes long even on a USB drive.</summary>
    public long ByteCap { get; init; } = 512L * 1024 * 1024;

    /// <summary>Files larger than this are never sampled: one of them could eat the whole byte cap.</summary>
    public long MaxFileBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>
    /// Files written more recently than this are left out. They may still be changing, and no backup can
    /// be expected to have them yet. Negative values disable the window (tests).
    /// </summary>
    public TimeSpan ChurnWindow { get; init; } = TimeSpan.FromHours(2);

    /// <summary>At least this many files from every top-level folder of the source, so no folder goes silently unchecked.</summary>
    public int PerFolderFloor { get; init; } = 2;

    /// <summary>Fixed seed for repeatable samples (tests). Null draws a fresh sample every drill.</summary>
    public int? Seed { get; init; }
}

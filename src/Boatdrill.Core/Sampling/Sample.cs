namespace Boatdrill.Core.Sampling;

/// <summary>The files a drill will verify for one source, and what the whole source looks like.</summary>
public sealed record Sample(
    Source Source,
    IReadOnlyList<SampledFile> Files,
    long Population,
    long PopulationBytes,
    long SkippedLargeFiles,
    long SkippedChurningFiles,
    DateTimeOffset? NewestWriteUtc)
{
    public long SampledBytes => Files.Sum(f => f.Length);
}

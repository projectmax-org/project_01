namespace Boatdrill.Core.Sampling;

/// <summary>One file chosen for a drill. The relative path uses the platform separator and starts below the source root.</summary>
public sealed record SampledFile(string RelativePath, string FullPath, long Length, DateTimeOffset LastWriteUtc);

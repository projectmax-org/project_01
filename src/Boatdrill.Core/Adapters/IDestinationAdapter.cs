using Boatdrill.Core.Sampling;

namespace Boatdrill.Core.Adapters;

public enum CopyState
{
    Found,
    Missing,
    /// <summary>A cloud placeholder: the name is on the disk, the bytes are not.</summary>
    Placeholder,
    Error,
}

/// <summary>What a destination knows about the copy of one sampled file before any byte is read.</summary>
public sealed record CopyLookup(CopyState State, string? Path, long Length, DateTimeOffset? LastWriteUtc, string? Detail)
{
    public static CopyLookup Missing(string path) => new(CopyState.Missing, path, 0, null, null);

    public static CopyLookup Failed(string path, string detail) => new(CopyState.Error, path, 0, null, detail);
}

public sealed record DestinationProbe(bool Reachable, string Detail, long? FreeBytes);

/// <summary>Where one source's copies live inside a destination, and how sure the adapter is about it.</summary>
public sealed record SourceBinding(Destination Destination, Source Source, MirrorLayout Layout, string BasePath, int ProbeHits, int ProbeTried);

public sealed record ReadBack(bool Success, long BytesRead, TimeSpan Elapsed, string? Sha256, string? Detail);

/// <summary>
/// Everything Boatdrill knows about one kind of destination. An adapter only ever reads from the
/// destination; the one place it writes is the scratch folder on this PC.
/// </summary>
public interface IDestinationAdapter
{
    DestinationKind Kind { get; }

    /// <summary>Is the destination there at all right now?</summary>
    Task<DestinationProbe> ProbeAsync(Destination destination, CancellationToken ct);

    /// <summary>Works out where this source's copies live in the destination.</summary>
    Task<SourceBinding> BindAsync(Destination destination, Sample sample, CancellationToken ct);

    /// <summary>Finds the copy of one sampled file without reading it.</summary>
    Task<CopyLookup> LocateAsync(SourceBinding binding, SampledFile file, CancellationToken ct);

    /// <summary>Restores the copy into the scratch file, hashing the bytes as they stream through.</summary>
    Task<ReadBack> ReadBackAsync(CopyLookup copy, string scratchPath, CancellationToken ct);
}

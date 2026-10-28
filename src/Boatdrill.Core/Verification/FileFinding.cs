namespace Boatdrill.Core.Verification;

public enum FileOutcome
{
    /// <summary>A copy exists and its bytes match the original.</summary>
    Verified,

    /// <summary>A copy exists but is older than the original: the backup has not caught up.</summary>
    Outdated,

    /// <summary>A copy exists and is not older than the original, yet its bytes differ. Corruption, or a bad copy.</summary>
    Corrupt,

    /// <summary>No copy at the expected place.</summary>
    Missing,

    /// <summary>A cloud placeholder: the name is there, the bytes are not on this disk.</summary>
    Placeholder,

    /// <summary>The copy or the original could not be read.</summary>
    Error,
}

/// <summary>What a drill found out about one sampled file at one destination.</summary>
public sealed record FileFinding(
    string RelativePath,
    FileOutcome Outcome,
    long SourceLength,
    DateTimeOffset SourceWriteUtc,
    long? CopyLength,
    DateTimeOffset? CopyWriteUtc,
    string? Detail);

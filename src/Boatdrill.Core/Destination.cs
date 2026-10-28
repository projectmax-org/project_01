namespace Boatdrill.Core;

public enum DestinationKind
{
    /// <summary>A folder that holds plain copies: an external drive, a NAS share, a OneDrive or Dropbox folder.</summary>
    MirrorFolder,
}

/// <summary>How a mirror destination lays out the copies of one source.</summary>
public enum MirrorLayout
{
    /// <summary>Work it out from the sample: try the others and keep the one that finds the most files.</summary>
    Auto,

    /// <summary>The destination root holds the source folder's contents directly: D:\Backup ↔ C:\Users\max\Documents.</summary>
    Contents,

    /// <summary>The destination root holds a folder named after the source: D:\Backup\Documents ↔ C:\Users\max\Documents.</summary>
    RootFolder,

    /// <summary>The destination mirrors the full path below a volume folder: D:\Backup\C\Users\max\Documents.</summary>
    FullPath,
}

/// <summary>A place where copies are supposed to exist. Boatdrill only ever reads from it.</summary>
public sealed record Destination(string Id, string Name, DestinationKind Kind, string RootPath)
{
    public MirrorLayout Layout { get; init; } = MirrorLayout.Auto;

    public static Destination MirrorFolder(string rootPath, string? name = null, MirrorLayout layout = MirrorLayout.Auto)
    {
        var full = Path.GetFullPath(rootPath);
        return new Destination(Source.IdForPath(full), name ?? Source.DisplayName(full), DestinationKind.MirrorFolder, full) { Layout = layout };
    }
}

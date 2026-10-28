namespace Boatdrill.Core.Drill;

public sealed record DrillProgress(string Stage, string? Destination, int FilesDone, int FilesTotal, long BytesRead);

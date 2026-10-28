namespace Boatdrill.Core;

/// <summary>Every on-disk location the app uses. Created on first access.</summary>
public static class Paths
{
    private static string? _data;

    /// <summary>%LOCALAPPDATA%\Boatdrill: settings, history, scratch.</summary>
    public static string DataDirectory => _data ??= EnsureDirectory(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Boatdrill"));

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>One JSON line per drill; see Boatdrill.Core.History.HistoryStore.</summary>
    public static string HistoryFile => Path.Combine(DataDirectory, "history.jsonl");

    /// <summary>Where sampled copies are restored to during a drill. Emptied again when the drill ends.</summary>
    public static string ScratchDirectory => EnsureDirectory(Path.Combine(DataDirectory, "scratch"));

    public static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }
}

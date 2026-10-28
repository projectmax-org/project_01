namespace Boatdrill.Core;

public enum Schedule
{
    Daily,
    Weekly,
    Monthly,
}

/// <summary>What the user asked Boatdrill to protect and where to look. Plain properties so it round-trips through JSON.</summary>
public sealed class AppSettings
{
    public List<SourceSetting> Sources { get; set; } = [];

    public List<DestinationSetting> Destinations { get; set; } = [];

    public Schedule Schedule { get; set; } = Schedule.Weekly;

    public int SampleSize { get; set; } = 200;

    public int ByteCapMb { get; set; } = 512;

    /// <summary>The folders most people mean when they say "my files". Destinations are theirs to add.</summary>
    public static AppSettings Defaults()
    {
        var settings = new AppSettings();
        foreach (var folder in new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.DesktopDirectory })
        {
            var path = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
            settings.Sources.Add(new SourceSetting
            {
                Id = Source.IdForPath(path),
                Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path)),
                Path = path,
                Excludes = ["Thumbs.db", "desktop.ini", "*.tmp", "~$*"],
            });
        }

        return settings;
    }
}

public sealed class SourceSetting
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Path { get; set; }

    public List<string> Excludes { get; set; } = [];

    public Source ToSource() => new(Id, Name, Path) { Excludes = Excludes };
}

public sealed class DestinationSetting
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Path { get; set; }

    public MirrorLayout Layout { get; set; } = MirrorLayout.Auto;

    public Destination ToDestination() => new(Id, Name, DestinationKind.MirrorFolder, Path) { Layout = Layout };
}

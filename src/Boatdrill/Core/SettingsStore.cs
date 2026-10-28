using System.Text.Json;

namespace Boatdrill.Core;

/// <summary>
/// Loads and saves <see cref="AppSettings"/>. Writes go through a temporary file so a power cut
/// mid-write cannot leave an unparsable settings file behind. A missing file gets the defaults, and the
/// defaults are written out so the user has something to edit until the settings screen lands.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;
        Settings = Load();
    }

    public AppSettings Settings { get; private set; }

    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Settings, BoatdrillJson.Indented));
        File.Move(temporary, _path, overwrite: true);
    }

    public void Reload() => Settings = Load();

    private AppSettings Load()
    {
        if (File.Exists(_path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), BoatdrillJson.Options);
                if (loaded is not null) return loaded;
            }
            catch (JsonException)
            {
                // Fall through to defaults; the broken file is kept beside the new one for the user.
                File.Copy(_path, _path + ".broken", overwrite: true);
            }
        }

        var defaults = AppSettings.Defaults();
        Settings = defaults;
        Save();
        return defaults;
    }
}

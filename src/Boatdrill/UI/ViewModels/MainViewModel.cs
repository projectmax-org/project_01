using System.Collections.ObjectModel;
using System.Windows;
using Boatdrill.Core;
using Boatdrill.Core.Drill;
using Boatdrill.Core.History;
using Boatdrill.Core.Sampling;
using Boatdrill.Core.Text;

namespace Boatdrill.UI.ViewModels;

/// <summary>
/// The dashboard. Reads settings and the last drill from disk, runs a drill on request and shows each
/// destination's score and sentence. Scheduling, the tray icon and the settings screen come with the
/// v0.1 app milestone; until then sources and destinations are edited in settings.json.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly SettingsStore _settings = new(Paths.SettingsFile);
    private readonly HistoryStore _history = new(Paths.HistoryFile);
    private bool _running;
    private double _progress;
    private string _status = string.Empty;
    private string _footer = string.Empty;

    public MainViewModel()
    {
        RunDrill = new RelayCommand(() => _ = RunAsync(), () => !_running);
        Load();
    }

    public ObservableCollection<DestinationRow> Destinations { get; } = [];

    public RelayCommand RunDrill { get; }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string Footer
    {
        get => _footer;
        private set => Set(ref _footer, value);
    }

    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public Visibility ProgressVisibility => _running ? Visibility.Visible : Visibility.Collapsed;

    private void Load()
    {
        var settings = _settings.Settings;
        var latest = _history.Latest();

        Destinations.Clear();
        foreach (var destination in settings.Destinations)
        {
            var row = new DestinationRow(destination.Id, destination.Name, destination.Path);
            var last = latest?.Destinations.FirstOrDefault(d => d.DestinationId == destination.Id);
            if (last is not null) row.Apply(last);
            Destinations.Add(row);
        }

        Status = settings.Destinations.Count == 0
            ? $"No destination yet. Until the settings screen lands, add destinations in {Paths.SettingsFile}."
            : latest is null
                ? "No drill has run yet."
                : $"Last drill {latest.StartedUtc.ToLocalTime():g}, took {Humanize.Duration(latest.Duration)}.";

        Footer = $"Protecting {Humanize.Plural(settings.Sources.Count, "folder")}: {string.Join(", ", settings.Sources.Select(s => s.Name))}. " +
                 "Nothing leaves this PC, and a drill only ever reads from a destination.";
    }

    private async Task RunAsync()
    {
        if (_running) return;
        SetRunning(true);
        try
        {
            var settings = _settings.Settings;
            var plan = new DrillPlan
            {
                Sources = settings.Sources.Select(s => s.ToSource()).ToList(),
                Destinations = settings.Destinations.Select(d => d.ToDestination()).ToList(),
                ScratchDirectory = Paths.ScratchDirectory,
                Sampling = new SampleOptions { SampleSize = settings.SampleSize, ByteCap = settings.ByteCapMb * 1024L * 1024 },
            };

            var runner = new DrillRunner(reachabilityHistory: id => _history.Reachability(id));
            var progress = new Progress<DrillProgress>(p =>
            {
                Status = p.FilesTotal > 0 ? $"{p.Stage}: {p.FilesDone} of {p.FilesTotal} files, {Humanize.Bytes(p.BytesRead)} read" : p.Stage;
                Progress = p.FilesTotal > 0 ? (double)p.FilesDone / p.FilesTotal : 0;
            });

            var report = await runner.RunAsync(plan, progress);
            await _history.AppendAsync(report);

            foreach (var result in report.Destinations)
            {
                Destinations.FirstOrDefault(r => r.Id == result.DestinationId)?.Apply(result);
            }

            Status = $"Drill finished at {report.FinishedUtc.ToLocalTime():t}, took {Humanize.Duration(report.Duration)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Status = "The drill failed: " + ex.Message;
        }
        finally
        {
            SetRunning(false);
            Progress = 0;
        }
    }

    private void SetRunning(bool running)
    {
        _running = running;
        RunDrill.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(ProgressVisibility));
    }
}

using System.Globalization;
using System.Text.Json;
using Boatdrill.Core;
using Boatdrill.Core.Drill;
using Boatdrill.Core.History;
using Boatdrill.Core.Sampling;
using Boatdrill.Core.Scoring;
using Boatdrill.Core.Text;

namespace Boatdrill.Cli;

public static class Program
{
    private const string Usage = """
        boatdrill-cli drill --source <folder> [--source <folder> ...] --dest <folder> [--dest <folder> ...]
                            [--sample <files>] [--cap-mb <MB>] [--seed <n>] [--churn-hours <h>]
                            [--scratch <folder>] [--history <file>] [--json]

        Rehearses restoring a sample of files from every source out of every destination and says whether
        the backup would work. It only reads from destinations; the one thing it writes is the scratch folder.

          --sample       files per source (200)        --cap-mb       most MB read per source (512)
          --seed         repeatable sample              --churn-hours  skip files written this recently (2)
          --scratch      where read-back lands (temp)   --history      append the report to this .jsonl file
          --json         print the full report as JSON instead of text

        Exit code 0 when every destination would restore (fully or mostly), 1 otherwise, 2 for bad arguments.
        """;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 2 : 0;
        }

        if (args[0] is "--version" or "version")
        {
            Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString(3));
            return 0;
        }

        if (args[0] != "drill")
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'.\n\n{Usage}");
            return 2;
        }

        var sources = new List<string>();
        var destinations = new List<string>();
        var sampleSize = 200;
        long capMb = 512;
        int? seed = null;
        var churnHours = 2.0;
        string? scratch = null;
        string? history = null;
        var json = false;

        for (var i = 1; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
            try
            {
                switch (args[i])
                {
                    case "--source": sources.Add(Next()); break;
                    case "--dest": destinations.Add(Next()); break;
                    case "--sample": sampleSize = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--cap-mb": capMb = long.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--seed": seed = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--churn-hours": churnHours = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--scratch": scratch = Next(); break;
                    case "--history": history = Next(); break;
                    case "--json": json = true; break;
                    default:
                        Console.Error.WriteLine($"Unknown option '{args[i]}'.\n\n{Usage}");
                        return 2;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        if (sources.Count == 0 || destinations.Count == 0)
        {
            Console.Error.WriteLine("At least one --source and one --dest are needed.\n\n" + Usage);
            return 2;
        }

        var store = history is null ? null : new HistoryStore(history);
        var plan = new DrillPlan
        {
            Sources = sources.Select(s => Source.FromPath(s)).ToList(),
            Destinations = destinations.Select(d => Destination.MirrorFolder(d)).ToList(),
            ScratchDirectory = scratch ?? Path.Combine(Path.GetTempPath(), "boatdrill"),
            Sampling = new SampleOptions
            {
                SampleSize = sampleSize,
                ByteCap = capMb * 1024 * 1024,
                Seed = seed,
                ChurnWindow = TimeSpan.FromHours(churnHours),
            },
        };

        var runner = new DrillRunner(reachabilityHistory: id => store?.Reachability(id));
        var report = await runner.RunAsync(plan, json ? null : new ConsoleProgress());
        if (!json) Console.Error.WriteLine();
        if (store is not null) await store.AppendAsync(report);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, BoatdrillJson.Indented));
        }
        else
        {
            Print(report);
        }

        return report.Worst <= Verdict.WouldMostlyRestore ? 0 : 1;
    }

    private static void Print(DrillReport report)
    {
        Console.WriteLine($"Boatdrill drill at {report.StartedUtc.ToLocalTime():yyyy-MM-dd HH:mm}: " +
                          $"{Humanize.Plural(report.Sources.Count, "source")}, {Humanize.Plural(report.Destinations.Count, "destination")}, " +
                          $"{Humanize.Duration(report.Duration)}.");

        foreach (var source in report.Sources)
        {
            var skipped = source.SkippedLargeFiles + source.SkippedChurningFiles;
            Console.WriteLine($"  {source.Name}: {source.Population:N0} files, {Humanize.Bytes(source.PopulationBytes)}; " +
                              $"sampled {source.Sampled:N0} ({Humanize.Bytes(source.SampledBytes)})" +
                              (skipped > 0 ? $"; left out {source.SkippedLargeFiles:N0} large and {source.SkippedChurningFiles:N0} just-changed files" : string.Empty));
        }

        foreach (var d in report.Destinations)
        {
            Console.WriteLine();
            Console.WriteLine($"{d.DestinationName}  ({d.RootPath})");
            Console.WriteLine($"  {d.Sentence}");
            if (!d.Reachable) continue;

            var s = d.Score;
            Console.WriteLine($"  Score {s.Total}: coverage {Percent(s.Coverage)} (±{Percent(s.CoverageMargin)}), freshness {Percent(s.Freshness)}, " +
                              $"integrity {Percent(s.Integrity)}, reachability {Percent(s.Reachability)}");
            if (d.BytesPerSecond is { } speed)
            {
                Console.WriteLine($"  Read back {Humanize.Bytes(d.BytesRead)} at {Humanize.Bytes((long)speed)}/s");
            }

            foreach (var b in d.Bindings)
            {
                Console.WriteLine($"  {b.SourceName} looked up under {b.BasePath} ({b.Layout}, {b.ProbeHits}/{b.ProbeTried} probe hits)");
            }

            foreach (var f in d.Findings.Take(20))
            {
                Console.WriteLine($"  {f.Outcome,-11} {f.RelativePath}" + (f.Detail is null ? string.Empty : $"  ({f.Detail})"));
            }

            if (d.Findings.Count > 20)
            {
                Console.WriteLine($"  ... and {d.Findings.Count - 20:N0} more; use --json for all of them");
            }
        }
    }

    private static string Percent(double share) => $"{Math.Round(share * 100)}%";

    /// <summary>Writes progress on the error stream, in place, so the report on stdout stays clean for pipes.</summary>
    private sealed class ConsoleProgress : IProgress<DrillProgress>
    {
        public void Report(DrillProgress p)
        {
            var line = p.FilesTotal > 0
                ? $"{p.Stage}: {p.FilesDone}/{p.FilesTotal} files, {Humanize.Bytes(p.BytesRead)}"
                : $"{p.Stage}...";
            Console.Error.Write("\r" + line.PadRight(78));
        }
    }
}

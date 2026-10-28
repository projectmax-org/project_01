using Boatdrill.Core.Drill;
using Boatdrill.Core.History;
using Boatdrill.Core.Sampling;
using Boatdrill.Core.Scoring;
using Boatdrill.Core.Verification;

namespace Boatdrill.Core.Tests;

public sealed class DrillRunnerTests
{
    private static readonly SampleOptions Everything = new()
    {
        SampleSize = 1000,
        Seed = 1,
        ChurnWindow = TimeSpan.FromHours(-1),
        PerFolderFloor = 0,
    };

    private static DrillPlan Plan(TempTree scratch, Source source, params Destination[] destinations) => new()
    {
        Sources = [source],
        Destinations = destinations,
        ScratchDirectory = scratch.Root,
        Sampling = Everything,
    };

    private static void WriteDocuments(TempTree tree, int count, DateTimeOffset when)
    {
        for (var i = 0; i < count; i++) tree.Write($"docs/{i}.txt", $"document {i}", when);
    }

    [Fact]
    public async Task CompleteMirror_WouldRestore()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 25, DateTimeOffset.UtcNow.AddDays(-2));
        tree.MirrorTo(backup.Root);

        var report = await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root, "Docs"), Destination.MirrorFolder(backup.Root, "Drive")));

        var result = Assert.Single(report.Destinations);
        Assert.True(result.Reachable);
        Assert.Equal(25, result.Sampled);
        Assert.Equal(25, result.Verified);
        Assert.Empty(result.Findings);
        Assert.Equal(100, result.Score.Total);
        Assert.Equal(Verdict.WouldRestore, result.Score.Verdict);
        Assert.StartsWith("Drive would restore.", result.Sentence);
        Assert.Contains("25 of 25 sampled files read back correctly", result.Sentence);
        Assert.Null(result.BehindSince);
        Assert.NotNull(result.EstimatedFullRestore);
        Assert.Equal(Verdict.WouldRestore, report.Worst);
        var summary = Assert.Single(report.Sources);
        Assert.Equal(25, summary.Population);
    }

    [Fact]
    public async Task MissingCopies_LowerCoverageAndSayHowMany()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 20, DateTimeOffset.UtcNow.AddDays(-2));
        tree.MirrorTo(backup.Root, skip: ["docs/0.txt", "docs/1.txt", "docs/2.txt", "docs/3.txt", "docs/4.txt"]);

        var report = await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root, "Docs"), Destination.MirrorFolder(backup.Root, "Drive")));

        var result = Assert.Single(report.Destinations);
        Assert.Equal(5, result.Missing);
        Assert.Equal(15, result.Verified);
        Assert.Equal(0.75, result.Score.Coverage, 3);
        Assert.Equal(0, result.Score.CoverageMargin); // every file was sampled, so there is no sampling error
        Assert.Equal(Verdict.WouldMostlyRestore, result.Score.Verdict);
        Assert.Contains("5 have no copy", result.Sentence);
        Assert.Equal(5, result.Findings.Count(f => f.Outcome == FileOutcome.Missing));
    }

    [Fact]
    public async Task CorruptCopy_IsFlaggedAndCapsTheScore()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        var when = DateTimeOffset.UtcNow.AddDays(-2);
        WriteDocuments(tree, 20, when);
        tree.MirrorTo(backup.Root);
        var damaged = backup.At(Path.Combine("docs", "3.txt"));
        File.WriteAllText(damaged, "document X"); // same length, different bytes
        File.SetLastWriteTimeUtc(damaged, when.UtcDateTime);

        var report = await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root, "Docs"), Destination.MirrorFolder(backup.Root, "Drive")));

        var result = Assert.Single(report.Destinations);
        Assert.Equal(1, result.Corrupt);
        Assert.Equal(19, result.Verified);
        Assert.True(result.Score.Total <= Scorer.CorruptionCap);
        Assert.True(result.Score.Verdict >= Verdict.NeedsAttention);
        Assert.Contains("points to corruption", result.Sentence);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(FileOutcome.Corrupt, finding.Outcome);
        Assert.Contains("same size and date", finding.Detail);
    }

    [Fact]
    public async Task OutdatedCopy_SetsBehindSince()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 20, DateTimeOffset.UtcNow.AddDays(-5));
        tree.MirrorTo(backup.Root);
        var edited = DateTimeOffset.UtcNow.AddHours(-1);
        tree.Write("docs/7.txt", "document 7, second draft", edited);

        var report = await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root, "Docs"), Destination.MirrorFolder(backup.Root, "Drive")));

        var result = Assert.Single(report.Destinations);
        Assert.Equal(1, result.Outdated);
        Assert.NotNull(result.BehindSince);
        Assert.Equal(edited.UtcDateTime, result.BehindSince!.Value.UtcDateTime, TimeSpan.FromSeconds(1));
        Assert.NotNull(result.Lag);
        Assert.True(result.Lag > TimeSpan.FromMinutes(50));
        Assert.True(result.Score.Freshness < 1);
        Assert.Contains("1 is older than the original", result.Sentence);
        Assert.Contains("The oldest change not yet copied here is from", result.Sentence);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(FileOutcome.Outdated, finding.Outcome);
    }

    [Fact]
    public async Task UnreachableDestination_SaysSo()
    {
        using var tree = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 3, DateTimeOffset.UtcNow.AddDays(-2));

        var report = await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root, "Docs"), Destination.MirrorFolder(scratch.At("unplugged"), "USB drive")));

        var result = Assert.Single(report.Destinations);
        Assert.False(result.Reachable);
        Assert.Equal(Verdict.Unreachable, result.Score.Verdict);
        Assert.Equal(0, result.Score.Total);
        Assert.StartsWith("USB drive could not be reached:", result.Sentence);
        Assert.Equal(Verdict.Unreachable, report.Worst);
    }

    [Fact]
    public async Task SourceNotInDestination_IsNamedInTheSentence()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 10, DateTimeOffset.UtcNow.AddDays(-2));
        backup.Write("something-else.txt", "unrelated");

        var report = await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root, "Docs"), Destination.MirrorFolder(backup.Root, "Drive")));

        var result = Assert.Single(report.Destinations);
        Assert.Equal(10, result.Missing);
        Assert.Equal(Verdict.WouldNotRestore, result.Score.Verdict);
        Assert.Contains($"No copy of Docs was found under {backup.Root}", result.Sentence);
    }

    [Fact]
    public async Task ScratchFolderIsRemovedAfterTheDrill()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 5, DateTimeOffset.UtcNow.AddDays(-2));
        tree.MirrorTo(backup.Root);

        await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root), Destination.MirrorFolder(backup.Root)));

        Assert.Empty(Directory.GetDirectories(scratch.Root, "boatdrill-*"));
    }

    [Fact]
    public async Task Progress_ReachesTheLastFile()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 8, DateTimeOffset.UtcNow.AddDays(-2));
        tree.MirrorTo(backup.Root);
        var seen = new List<DrillProgress>();

        await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root), Destination.MirrorFolder(backup.Root)), new SynchronousProgress(seen));

        Assert.Contains(seen, p => p.FilesTotal == 8 && p.FilesDone == 8);
    }

    [Fact]
    public async Task History_RoundTripsAndFeedsReachability()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        using var scratch = new TempTree();
        WriteDocuments(tree, 5, DateTimeOffset.UtcNow.AddDays(-2));
        tree.MirrorTo(backup.Root);
        var destination = Destination.MirrorFolder(backup.Root, "Drive");
        var store = new HistoryStore(scratch.At(Path.Combine("data", "history.jsonl")));

        var report = await new DrillRunner().RunAsync(Plan(scratch, Source.FromPath(tree.Root), destination));
        await store.AppendAsync(report);
        File.AppendAllText(store.FilePath, "{ torn line");

        var read = Assert.Single(store.ReadAll());
        Assert.Equal(report.Id, read.Id);
        Assert.Equal(report.Destinations[0].Sentence, read.Destinations[0].Sentence);
        Assert.Equal(report.Destinations[0].Score, read.Destinations[0].Score);
        Assert.Equal(1.0, store.Reachability(destination.Id));
        Assert.Null(store.Reachability("never-seen"));
        Assert.Equal(report.Id, store.Latest()?.Id);
    }

    [Fact]
    public void Reachability_BlendsHistoryWithNow()
    {
        Assert.Equal(1.0, DrillRunner.Reachability(null, true));
        Assert.Equal(0.0, DrillRunner.Reachability(null, false));
        Assert.Equal(0.7, DrillRunner.Reachability(1.0, false), 3);
        Assert.Equal(0.3, DrillRunner.Reachability(0.0, true), 3);
    }

    private sealed class SynchronousProgress(List<DrillProgress> sink) : IProgress<DrillProgress>
    {
        public void Report(DrillProgress value) => sink.Add(value);
    }
}

using Boatdrill.Core.Sampling;

namespace Boatdrill.Core.Tests;

public sealed class SamplerTests
{
    private static SampleOptions Options(int size, int? seed = 1, long cap = long.MaxValue, int floor = 2) => new()
    {
        SampleSize = size,
        Seed = seed,
        ByteCap = cap,
        PerFolderFloor = floor,
        ChurnWindow = TimeSpan.FromHours(-1),
    };

    [Fact]
    public void Take_IsRepeatableWithASeed()
    {
        using var tree = new TempTree();
        for (var i = 0; i < 50; i++) tree.Write($"f/{i}.txt", $"file {i}");
        var source = Source.FromPath(tree.Root);

        var first = new Sampler(Options(10, seed: 7)).Take(source);
        var second = new Sampler(Options(10, seed: 7)).Take(source);

        Assert.Equal(first.Files.Select(f => f.RelativePath), second.Files.Select(f => f.RelativePath));
        Assert.Equal(50, first.Population);
        Assert.InRange(first.Files.Count, 10, 12);
    }

    [Fact]
    public void Take_RespectsTheByteCap()
    {
        using var tree = new TempTree();
        for (var i = 0; i < 20; i++) tree.Write($"{i}.bin", new string('x', 1000));

        var sample = new Sampler(Options(20, cap: 5000, floor: 0)).Take(Source.FromPath(tree.Root));

        Assert.True(sample.SampledBytes <= 5000);
        Assert.Equal(5, sample.Files.Count);
        Assert.Equal(20_000, sample.PopulationBytes);
    }

    [Fact]
    public void Take_CoversEveryTopLevelFolder()
    {
        using var tree = new TempTree();
        for (var folder = 0; folder < 10; folder++)
        {
            for (var i = 0; i < 30; i++) tree.Write($"folder{folder}/{i}.txt", "x");
        }

        var sample = new Sampler(Options(12, floor: 1)).Take(Source.FromPath(tree.Root));

        var folders = sample.Files.Select(f => f.RelativePath.Split('/', '\\')[0]).Distinct().Count();
        Assert.Equal(10, folders);
    }

    [Fact]
    public void Take_FavoursRecentFiles()
    {
        using var tree = new TempTree();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 900; i++) tree.Write($"old/{i}.txt", "o", now.AddDays(-400));
        for (var i = 0; i < 100; i++) tree.Write($"new/{i}.txt", "n", now.AddDays(-1));

        var sample = new Sampler(Options(100, seed: 3, floor: 0)).Take(Source.FromPath(tree.Root));

        // Weights 4 against 0.5: the 100 recent files carry 400 of 850 units, so expect about 45 of 100.
        // A uniform draw would give about 10.
        var recent = sample.Files.Count(f => f.RelativePath.StartsWith("new", StringComparison.Ordinal));
        Assert.InRange(recent, 25, 70);
    }

    [Fact]
    public void Take_SkipsLargeAndChurningFilesButCountsThem()
    {
        using var tree = new TempTree();
        tree.Write("big.bin", new string('x', 5000), DateTimeOffset.UtcNow.AddDays(-3));
        tree.Write("fresh.txt", "just written");
        tree.Write("ok.txt", "fine", DateTimeOffset.UtcNow.AddDays(-3));
        var options = new SampleOptions { SampleSize = 10, Seed = 1, MaxFileBytes = 1000, ChurnWindow = TimeSpan.FromHours(1) };

        var sample = new Sampler(options).Take(Source.FromPath(tree.Root));

        Assert.Equal(3, sample.Population);
        Assert.Equal(1, sample.SkippedLargeFiles);
        Assert.Equal(1, sample.SkippedChurningFiles);
        var only = Assert.Single(sample.Files);
        Assert.Equal("ok.txt", only.RelativePath);
    }

    [Fact]
    public void Take_AppliesExcludes()
    {
        using var tree = new TempTree();
        tree.Write("node_modules/x/a.js", "a");
        tree.Write("src/a.tmp", "t");
        tree.Write("src/keep.cs", "k");
        var source = Source.FromPath(tree.Root) with { Excludes = ["node_modules", "*.tmp"] };

        var sample = new Sampler(Options(10)).Take(source);

        var only = Assert.Single(sample.Files);
        Assert.EndsWith("keep.cs", only.RelativePath);
        Assert.Equal(1, sample.Population);
    }

    [Fact]
    public void Take_MissingRootGivesAnEmptySample()
    {
        var sample = new Sampler(Options(10)).Take(Source.FromPath(Path.Combine(Path.GetTempPath(), "boatdrill-does-not-exist")));

        Assert.Empty(sample.Files);
        Assert.Equal(0, sample.Population);
    }

    [Fact]
    public void Weight_FavoursRecentFiles()
    {
        Assert.True(Sampler.Weight(TimeSpan.FromDays(1)) > Sampler.Weight(TimeSpan.FromDays(20)));
        Assert.True(Sampler.Weight(TimeSpan.FromDays(20)) > Sampler.Weight(TimeSpan.FromDays(400)));
    }
}

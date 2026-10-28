using Boatdrill.Core.Adapters;
using Boatdrill.Core.Sampling;

namespace Boatdrill.Core.Tests;

public sealed class MirrorFolderAdapterTests
{
    private static readonly SampleOptions Everything = new() { SampleSize = 100, Seed = 1, ChurnWindow = TimeSpan.FromHours(-1) };

    [Theory]
    [InlineData(MirrorLayout.Contents)]
    [InlineData(MirrorLayout.RootFolder)]
    [InlineData(MirrorLayout.FullPath)]
    public async Task Bind_DetectsTheLayoutFromTheSample(MirrorLayout layout)
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        for (var i = 0; i < 10; i++) tree.Write($"a/{i}.txt", $"{i}");
        var source = Source.FromPath(tree.Root);
        var destination = Destination.MirrorFolder(backup.Root);
        tree.MirrorTo(MirrorFolderAdapter.Candidates(destination, source).First(c => c.Layout == layout).BasePath);

        var sample = new Sampler(Everything).Take(source);
        var binding = await new MirrorFolderAdapter().BindAsync(destination, sample, CancellationToken.None);

        Assert.Equal(layout, binding.Layout);
        Assert.Equal(10, binding.ProbeHits);
        Assert.Equal(10, binding.ProbeTried);
    }

    [Fact]
    public async Task Bind_HonoursAFixedLayout()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        tree.Write("x.txt", "x");
        var source = Source.FromPath(tree.Root);
        var destination = Destination.MirrorFolder(backup.Root, layout: MirrorLayout.RootFolder);

        var binding = await new MirrorFolderAdapter().BindAsync(destination, new Sampler(Everything).Take(source), CancellationToken.None);

        Assert.Equal(MirrorLayout.RootFolder, binding.Layout);
        Assert.Equal(Path.Combine(backup.Root, Path.GetFileName(tree.Root)), binding.BasePath);
    }

    [Fact]
    public async Task Locate_ReportsAMissingCopy()
    {
        using var tree = new TempTree();
        using var backup = new TempTree();
        tree.Write("x.txt", "x");
        var source = Source.FromPath(tree.Root);
        var destination = Destination.MirrorFolder(backup.Root, layout: MirrorLayout.Contents);
        var adapter = new MirrorFolderAdapter();
        var sample = new Sampler(Everything).Take(source);
        var binding = await adapter.BindAsync(destination, sample, CancellationToken.None);

        var copy = await adapter.LocateAsync(binding, sample.Files[0], CancellationToken.None);

        Assert.Equal(CopyState.Missing, copy.State);
        Assert.Equal(Path.Combine(backup.Root, "x.txt"), copy.Path);
    }

    [Fact]
    public async Task ReadBack_RestoresTheCopyAndHashesIt()
    {
        using var backup = new TempTree();
        using var scratch = new TempTree();
        var copyPath = backup.Write("hello.txt", "hello");
        var copy = new CopyLookup(CopyState.Found, copyPath, 5, DateTimeOffset.UtcNow, null);
        var scratchFile = scratch.At("restored");

        var readBack = await new MirrorFolderAdapter().ReadBackAsync(copy, scratchFile, CancellationToken.None);

        Assert.True(readBack.Success);
        Assert.Equal(5, readBack.BytesRead);
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", readBack.Sha256);
        Assert.Equal("hello", File.ReadAllText(scratchFile));
    }

    [Fact]
    public async Task Probe_MissingRootIsUnreachable()
    {
        var destination = Destination.MirrorFolder(Path.Combine(Path.GetTempPath(), "boatdrill-not-here"));

        var probe = await new MirrorFolderAdapter().ProbeAsync(destination, CancellationToken.None);

        Assert.False(probe.Reachable);
        Assert.Contains("was not found", probe.Detail);
    }

    [Fact]
    public void VolumeFolder_NamesTheVolumeWithoutPunctuation()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("C", MirrorFolderAdapter.VolumeFolder(@"C:\Users\max\Documents"));
            Assert.Equal(@"Users\max\Documents", MirrorFolderAdapter.PathBelowVolume(@"C:\Users\max\Documents"));
        }
        else
        {
            Assert.Equal("root", MirrorFolderAdapter.VolumeFolder("/home/max/Documents"));
            Assert.Equal("home/max/Documents", MirrorFolderAdapter.PathBelowVolume("/home/max/Documents"));
        }
    }
}

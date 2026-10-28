using System.Diagnostics;
using System.Security.Cryptography;
using Boatdrill.Core.Sampling;

namespace Boatdrill.Core.Adapters;

/// <summary>Plain copies in a folder: an external drive, a NAS share, a OneDrive or Dropbox folder.</summary>
public sealed class MirrorFolderAdapter : IDestinationAdapter
{
    // Windows marks a cloud placeholder with these; the FileAttributes enum lacks the last two.
    private const int RecallOnOpen = 0x40000;
    private const int RecallOnDataAccess = 0x400000;

    /// <summary>Sampled files tried against each layout before choosing one.</summary>
    private const int ProbeFiles = 25;

    private const int BufferSize = 1 << 16;

    public DestinationKind Kind => DestinationKind.MirrorFolder;

    public Task<DestinationProbe> ProbeAsync(Destination destination, CancellationToken ct)
    {
        var root = destination.RootPath;
        if (!Directory.Exists(root))
        {
            return Task.FromResult(new DestinationProbe(false, $"{root} was not found", null));
        }

        long? free = null;
        try
        {
            free = new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Network paths and unusual mounts give no free-space figure; the destination is still reachable.
        }

        return Task.FromResult(new DestinationProbe(true, $"{root} is reachable", free));
    }

    public Task<SourceBinding> BindAsync(Destination destination, Sample sample, CancellationToken ct)
    {
        var candidates = Candidates(destination, sample.Source).ToList();
        if (destination.Layout != MirrorLayout.Auto)
        {
            var (layout, basePath) = candidates.First(c => c.Layout == destination.Layout);
            return Task.FromResult(new SourceBinding(destination, sample.Source, layout, basePath, 0, 0));
        }

        var probe = sample.Files.Take(ProbeFiles).ToList();
        SourceBinding? best = null;
        foreach (var (layout, basePath) in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var hits = probe.Count(f => File.Exists(Path.Combine(basePath, f.RelativePath)));
            if (best is null || hits > best.ProbeHits)
            {
                best = new SourceBinding(destination, sample.Source, layout, basePath, hits, probe.Count);
            }
        }

        return Task.FromResult(best!);
    }

    /// <summary>The layouts tried, in order; ties go to the earlier one.</summary>
    internal static IEnumerable<(MirrorLayout Layout, string BasePath)> Candidates(Destination destination, Source source)
    {
        var root = Path.GetFullPath(destination.RootPath);
        var sourceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.RootPath));
        yield return (MirrorLayout.Contents, root);
        yield return (MirrorLayout.RootFolder, Path.Combine(root, Source.DisplayName(sourceRoot)));
        yield return (MirrorLayout.FullPath, Path.Combine(root, VolumeFolder(sourceRoot), PathBelowVolume(sourceRoot)));
    }

    /// <summary>"C:\Users\max" gives "C"; "/home/max" gives "root". The folder a full-path mirror puts a volume under.</summary>
    internal static string VolumeFolder(string fullPath)
    {
        var volume = Path.GetPathRoot(fullPath) ?? string.Empty;
        var name = volume.Replace(":", string.Empty).Trim('\\', '/');
        return string.IsNullOrEmpty(name) ? "root" : name;
    }

    internal static string PathBelowVolume(string fullPath)
    {
        var volume = Path.GetPathRoot(fullPath) ?? string.Empty;
        return fullPath[volume.Length..].TrimStart('\\', '/');
    }

    public Task<CopyLookup> LocateAsync(SourceBinding binding, SampledFile file, CancellationToken ct)
    {
        var path = Path.Combine(binding.BasePath, file.RelativePath);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return Task.FromResult(CopyLookup.Missing(path));
            }

            var attributes = (int)info.Attributes;
            if ((attributes & (RecallOnDataAccess | RecallOnOpen)) != 0 || info.Attributes.HasFlag(FileAttributes.Offline))
            {
                return Task.FromResult(new CopyLookup(CopyState.Placeholder, path, info.Length, info.LastWriteTimeUtc,
                    "cloud placeholder: the bytes are not on this disk"));
            }

            return Task.FromResult(new CopyLookup(CopyState.Found, path, info.Length, info.LastWriteTimeUtc, null));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(CopyLookup.Failed(path, ex.Message));
        }
    }

    public async Task<ReadBack> ReadBackAsync(CopyLookup copy, string scratchPath, CancellationToken ct)
    {
        if (copy.State != CopyState.Found || copy.Path is null)
        {
            return new ReadBack(false, 0, TimeSpan.Zero, null, "no copy to read");
        }

        var watch = Stopwatch.StartNew();
        try
        {
            await using var input = new FileStream(copy.Path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = new FileStream(scratchPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize,
                FileOptions.Asynchronous);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            var buffer = new byte[BufferSize];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                total += read;
            }

            await output.FlushAsync(ct).ConfigureAwait(false);
            watch.Stop();
            return new ReadBack(true, total, watch.Elapsed, Convert.ToHexStringLower(hash.GetHashAndReset()), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            watch.Stop();
            return new ReadBack(false, 0, watch.Elapsed, null, ex.Message);
        }
    }
}

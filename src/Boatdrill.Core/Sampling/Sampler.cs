namespace Boatdrill.Core.Sampling;

/// <summary>
/// Picks the files a drill reads back, in one pass over the source.
/// <para>
/// Weighted reservoir sampling (Efraimidis and Spirakis): every file gets the key u^(1/w) for a uniform
/// u and a weight w that favours recently changed files, because those are the ones a backup is most
/// likely to be missing. The files with the largest keys are the sample. A small reservoir per top-level
/// folder guarantees every folder is represented. Memory is bounded by the sample size, not by the number
/// of files, so a terabyte of photos costs the same as a folder of documents.
/// </para>
/// </summary>
public sealed class Sampler(SampleOptions? options = null, TimeProvider? clock = null)
{
    private static readonly char[] Separators = ['/', '\\'];
    private readonly SampleOptions _options = options ?? new SampleOptions();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public Sample Take(Source source)
    {
        var root = Path.GetFullPath(source.RootPath);
        if (!Directory.Exists(root))
        {
            return new Sample(source, [], 0, 0, 0, 0, null);
        }

        var random = _options.Seed is int seed ? new Random(seed) : Random.Shared;
        var filter = new PathFilter(source.Excludes);
        var now = _clock.GetUtcNow();
        var churnCutoff = now - _options.ChurnWindow;

        var global = new Reservoir(_options.SampleSize);
        var perFolder = new Dictionary<string, Reservoir>(StringComparer.OrdinalIgnoreCase);

        long population = 0, populationBytes = 0, skippedLarge = 0, skippedChurning = 0;
        DateTimeOffset? newest = null;

        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            // Hidden and system files are mostly churn (desktop.ini, Thumbs.db); reparse points are
            // links and cloud placeholders, which a drill must never follow or pull down.
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };

        foreach (var info in new DirectoryInfo(root).EnumerateFiles("*", enumeration))
        {
            var relative = Path.GetRelativePath(root, info.FullName);
            if (filter.Excludes(relative)) continue;

            long length;
            DateTimeOffset written;
            try
            {
                length = info.Length;
                written = info.LastWriteTimeUtc;
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            population++;
            populationBytes += length;
            if (newest is null || written > newest) newest = written;

            if (length > _options.MaxFileBytes) { skippedLarge++; continue; }
            if (written > churnCutoff) { skippedChurning++; continue; }

            var file = new SampledFile(relative, info.FullName, length, written);
            var key = Math.Pow(random.NextDouble(), 1.0 / Weight(now - written));

            global.Offer(file, key);

            if (_options.PerFolderFloor > 0)
            {
                var folder = TopFolder(relative);
                if (!perFolder.TryGetValue(folder, out var reservoir))
                {
                    perFolder[folder] = reservoir = new Reservoir(_options.PerFolderFloor);
                }

                reservoir.Offer(file, key);
            }
        }

        var chosen = new Dictionary<string, SampledFile>(StringComparer.Ordinal);
        foreach (var file in global.Items) chosen[file.RelativePath] = file;
        foreach (var reservoir in perFolder.Values)
        {
            foreach (var file in reservoir.Items) chosen[file.RelativePath] = file;
        }

        var files = ApplyByteCap(chosen.Values.OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToList(), _options.ByteCap);
        return new Sample(source, files, population, populationBytes, skippedLarge, skippedChurning, newest);
    }

    /// <summary>Recent files weigh more: a file changed this week is eight times as likely to be drawn as one untouched for a year.</summary>
    internal static double Weight(TimeSpan age) => age.TotalDays switch
    {
        < 7 => 4.0,
        < 30 => 2.0,
        < 365 => 1.0,
        _ => 0.5,
    };

    private static string TopFolder(string relativePath)
    {
        var index = relativePath.IndexOfAny(Separators);
        return index < 0 ? string.Empty : relativePath[..index];
    }

    /// <summary>Drops the largest files until the sample fits the cap, always keeping at least one file.</summary>
    private static List<SampledFile> ApplyByteCap(List<SampledFile> files, long cap)
    {
        var total = files.Sum(f => f.Length);
        if (total <= cap) return files;

        var removed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files.OrderByDescending(f => f.Length))
        {
            if (total <= cap || files.Count - removed.Count <= 1) break;
            total -= file.Length;
            removed.Add(file.RelativePath);
        }

        return files.Where(f => !removed.Contains(f.RelativePath)).ToList();
    }

    /// <summary>Keeps the items with the largest keys, up to its capacity.</summary>
    private sealed class Reservoir(int capacity)
    {
        private readonly PriorityQueue<SampledFile, double> _queue = new();

        public void Offer(SampledFile item, double key)
        {
            if (capacity <= 0) return;
            if (_queue.Count < capacity)
            {
                _queue.Enqueue(item, key);
                return;
            }

            if (_queue.TryPeek(out _, out var smallest) && key > smallest)
            {
                _queue.Dequeue();
                _queue.Enqueue(item, key);
            }
        }

        public IEnumerable<SampledFile> Items => _queue.UnorderedItems.Select(e => e.Element);
    }
}

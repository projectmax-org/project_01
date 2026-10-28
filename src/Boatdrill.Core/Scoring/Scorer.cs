namespace Boatdrill.Core.Scoring;

public sealed record ScoreInputs(
    int Sampled,
    int Verified,
    int Outdated,
    int Corrupt,
    int Missing,
    int Placeholder,
    int Errors,
    long Population,
    TimeSpan? Lag,
    bool Reachable,
    double Reachability)
{
    /// <summary>Files that have a copy on the destination's disk, whatever its state.</summary>
    public int Found => Verified + Outdated + Corrupt;
}

/// <summary>
/// Coverage multiplies everything: a backup that holds half the sampled files cannot score above 50,
/// whatever else is true. Freshness, integrity and reachability share the rest with a fixed base.
/// docs/SCORING.md walks through the arithmetic with examples.
/// </summary>
public static class Scorer
{
    public const double BaseWeight = 0.60;
    public const double FreshnessWeight = 0.20;
    public const double IntegrityWeight = 0.10;
    public const double ReachabilityWeight = 0.10;

    /// <summary>A backup this far behind scores zero for freshness.</summary>
    public static readonly TimeSpan FreshnessHorizon = TimeSpan.FromDays(30);

    /// <summary>Any corruption caps the total below "would restore".</summary>
    public const int CorruptionCap = 69;

    public const int WouldRestoreFrom = 95;
    public const int WouldMostlyRestoreFrom = 70;
    public const int NeedsAttentionFrom = 40;

    public static ReadinessScore Score(ScoreInputs i)
    {
        if (!i.Reachable)
        {
            return new ReadinessScore(0, 0, 0, i.Reachability, 0, 0, Verdict.Unreachable);
        }

        if (i.Sampled == 0)
        {
            return new ReadinessScore(0, 0, 0, i.Reachability, 0, 0, Verdict.NothingToCheck);
        }

        var coverage = (double)i.Found / i.Sampled;
        var freshness = i.Lag is null ? 1.0 : Math.Clamp(1.0 - i.Lag.Value.TotalDays / FreshnessHorizon.TotalDays, 0, 1);
        var integrity = i.Found == 0 ? 0.0 : (double)(i.Found - i.Corrupt) / i.Found;
        var reachability = Math.Clamp(i.Reachability, 0, 1);

        var rest = BaseWeight + FreshnessWeight * freshness + IntegrityWeight * integrity + ReachabilityWeight * reachability;
        var total = (int)Math.Round(100 * coverage * rest);
        if (i.Corrupt > 0) total = Math.Min(total, CorruptionCap);

        var verdict = total switch
        {
            >= WouldRestoreFrom => Verdict.WouldRestore,
            >= WouldMostlyRestoreFrom => Verdict.WouldMostlyRestore,
            >= NeedsAttentionFrom => Verdict.NeedsAttention,
            _ => Verdict.WouldNotRestore,
        };

        return new ReadinessScore(coverage, freshness, integrity, reachability, total, Margin(coverage, i.Sampled, i.Population), verdict);
    }

    /// <summary>
    /// Half-width of the 95% interval for a share estimated from a sample of n files out of N, with the
    /// finite-population correction. 200 of 10,000 files at 95% coverage gives about ±3%; a census gives 0.
    /// </summary>
    public static double Margin(double share, int sampled, long population)
    {
        if (sampled <= 0 || sampled >= population) return 0;
        var p = Math.Clamp(share, 0, 1);
        var variance = p * (1 - p) / sampled * ((double)(population - sampled) / (population - 1));
        return 1.96 * Math.Sqrt(variance);
    }
}

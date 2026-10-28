using Boatdrill.Core.Scoring;

namespace Boatdrill.Core.Tests;

public sealed class ScorerTests
{
    private static ScoreInputs Inputs(int sampled = 200, int verified = 200, int outdated = 0, int corrupt = 0, int missing = 0,
        TimeSpan? lag = null, bool reachable = true, double reachability = 1.0) =>
        new(sampled, verified, outdated, corrupt, missing, 0, 0, 10_000, lag, reachable, reachability);

    [Fact]
    public void PerfectInputsScore100()
    {
        var score = Scorer.Score(Inputs());

        Assert.Equal(100, score.Total);
        Assert.Equal(Verdict.WouldRestore, score.Verdict);
        Assert.Equal(0, score.CoverageMargin);
    }

    [Fact]
    public void CorruptionCapsTheTotal()
    {
        var score = Scorer.Score(Inputs(verified: 199, corrupt: 1));

        Assert.True(score.Total <= Scorer.CorruptionCap);
        Assert.Equal(Verdict.NeedsAttention, score.Verdict);
        Assert.Equal(0.995, score.Integrity, 3);
    }

    [Fact]
    public void UnreachableScoresZero()
    {
        var score = Scorer.Score(Inputs(reachable: false, reachability: 0.4));

        Assert.Equal(0, score.Total);
        Assert.Equal(Verdict.Unreachable, score.Verdict);
        Assert.Equal(0.4, score.Reachability);
    }

    [Fact]
    public void NothingSampledIsNothingToCheck()
    {
        Assert.Equal(Verdict.NothingToCheck, Scorer.Score(Inputs(sampled: 0, verified: 0)).Verdict);
    }

    [Fact]
    public void FreshnessFallsWithLag()
    {
        Assert.Equal(0.5, Scorer.Score(Inputs(lag: TimeSpan.FromDays(15))).Freshness, 3);
        Assert.Equal(0.0, Scorer.Score(Inputs(lag: TimeSpan.FromDays(45))).Freshness, 3);
        Assert.Equal(1.0, Scorer.Score(Inputs(lag: TimeSpan.Zero)).Freshness, 3);
    }

    [Fact]
    public void MissingFilesLowerCoverageIntoTheBands()
    {
        Assert.Equal(Verdict.WouldMostlyRestore, Scorer.Score(Inputs(verified: 150, missing: 50)).Verdict);
        Assert.Equal(Verdict.NeedsAttention, Scorer.Score(Inputs(verified: 100, missing: 100)).Verdict);
        Assert.Equal(Verdict.WouldNotRestore, Scorer.Score(Inputs(verified: 0, missing: 200)).Verdict);
    }

    [Fact]
    public void MarginShrinksWithTheSampleAndVanishesForACensus()
    {
        Assert.True(Scorer.Margin(0.95, 50, 10_000) > Scorer.Margin(0.95, 400, 10_000));
        Assert.Equal(0, Scorer.Margin(0.9, 100, 100));
        Assert.Equal(0.03, Scorer.Margin(0.95, 200, 10_000), 2);
    }
}

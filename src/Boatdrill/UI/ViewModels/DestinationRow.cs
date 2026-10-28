using Boatdrill.Core.Drill;
using Boatdrill.Core.Scoring;

namespace Boatdrill.UI.ViewModels;

/// <summary>One destination on the dashboard: its name, its last score and its last sentence.</summary>
public sealed class DestinationRow(string id, string name, string rootPath) : ObservableObject
{
    private string _sentence = "No drill has run yet.";
    private string _scoreText = "–";
    private string _verdictText = string.Empty;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public string RootPath { get; } = rootPath;

    public string Sentence
    {
        get => _sentence;
        private set => Set(ref _sentence, value);
    }

    public string ScoreText
    {
        get => _scoreText;
        private set => Set(ref _scoreText, value);
    }

    public string VerdictText
    {
        get => _verdictText;
        private set => Set(ref _verdictText, value);
    }

    public void Apply(DestinationResult result)
    {
        Sentence = result.Sentence;
        ScoreText = result.Reachable ? result.Score.Total.ToString() : "–";
        VerdictText = result.Score.Verdict switch
        {
            Verdict.WouldRestore => "would restore",
            Verdict.WouldMostlyRestore => "would mostly restore",
            Verdict.NeedsAttention => "needs attention",
            Verdict.WouldNotRestore => "would not restore",
            Verdict.Unreachable => "unreachable",
            _ => "nothing to check",
        };
    }
}

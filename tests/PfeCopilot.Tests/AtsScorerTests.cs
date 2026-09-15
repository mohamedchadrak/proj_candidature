using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Application.Applications;
using Xunit;

namespace PfeCopilot.Tests;

public class AtsScorerTests
{
    [Fact]
    public void Score_Returns100_WhenAllKeywordsPresentAndNoRiskyLayout()
    {
        const string latex = "\\section*{Profil}\nDéveloppeur C# Azure";
        const string plainText = "Développeur C# Azure";
        var requirements = new JobRequirements { AtsKeywords = ["C#", "Azure"] };

        var result = AtsScorer.Score(latex, plainText, requirements);

        Assert.Equal(100, result.ScorePercent);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Score_FlagsMissingKeyword_WithoutBlocking()
    {
        const string latex = "\\section*{Profil}\nDéveloppeur C#";
        const string plainText = "Développeur C#";
        var requirements = new JobRequirements { AtsKeywords = ["C#", "Kubernetes"] };

        var result = AtsScorer.Score(latex, plainText, requirements);

        Assert.Equal(50, result.ScorePercent);
        Assert.Single(result.Findings);
        Assert.False(result.Findings[0].IsBlocking);
    }

    [Fact]
    public void Score_PenalizesRiskyLatexConstructs()
    {
        const string latex = "\\begin{tabular}{cc}A & B\\end{tabular}";
        const string plainText = "A B";
        var requirements = new JobRequirements { AtsKeywords = [] };

        var result = AtsScorer.Score(latex, plainText, requirements);

        Assert.True(result.ScorePercent < 100);
        Assert.Contains(result.Findings, f => f.IsBlocking);
    }
}

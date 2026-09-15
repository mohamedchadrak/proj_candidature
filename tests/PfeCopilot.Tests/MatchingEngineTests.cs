using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Application.Applications;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;
using Xunit;

namespace PfeCopilot.Tests;

public class MatchingEngineTests
{
    private static CvFact MakeFact(string title, string tags, string? description = null) => new()
    {
        UserId = "user-1",
        Type = CvFactType.CompetenceTechnique,
        Title = title,
        Description = description,
        Tags = tags
    };

    [Fact]
    public void Score_ReturnsZero_WhenNoKeywordsRequired()
    {
        var facts = new[] { MakeFact("C#", "C#,.NET") };
        var requirements = new JobRequirements();

        var result = MatchingEngine.Score(facts, requirements);

        Assert.Equal(0, result.ScorePercent);
        Assert.Empty(result.RankedFacts);
    }

    [Fact]
    public void Score_Returns100_WhenAllKeywordsMatched()
    {
        var facts = new[] { MakeFact("Développement .NET MAUI", "C#,.NET MAUI,Azure") };
        var requirements = new JobRequirements { HardSkills = ["C#", "Azure"] };

        var result = MatchingEngine.Score(facts, requirements);

        Assert.Equal(100, result.ScorePercent);
        Assert.Empty(result.UnmatchedKeywords);
        Assert.Single(result.RankedFacts);
    }

    [Fact]
    public void Score_ReportsUnmatchedKeywords_WhenPartiallyCovered()
    {
        var facts = new[] { MakeFact("Développement .NET MAUI", "C#,.NET MAUI") };
        var requirements = new JobRequirements { HardSkills = ["C#", "Kubernetes"] };

        var result = MatchingEngine.Score(facts, requirements);

        Assert.Equal(50, result.ScorePercent);
        Assert.Contains("Kubernetes", result.UnmatchedKeywords);
    }

    [Fact]
    public void Score_RanksFactsByRelevanceDescending()
    {
        var strongMatch = MakeFact("Azure DevOps CI/CD", "Azure,CI/CD,Git");
        var weakMatch = MakeFact("Excel", "Excel");
        var requirements = new JobRequirements { HardSkills = ["Azure", "CI/CD", "Git"] };

        var result = MatchingEngine.Score([weakMatch, strongMatch], requirements);

        Assert.Equal(strongMatch, result.RankedFacts[0].Fact);
    }
}

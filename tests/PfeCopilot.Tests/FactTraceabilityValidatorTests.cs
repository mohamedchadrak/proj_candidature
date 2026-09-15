using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Application.Applications;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;
using Xunit;

namespace PfeCopilot.Tests;

public class FactTraceabilityValidatorTests
{
    private static CvFact MakeFact(string title, string description = "") => new()
    {
        UserId = "user-1",
        Type = CvFactType.Experience,
        Title = title,
        Description = description
    };

    [Fact]
    public void Validate_ReturnsNoIssues_WhenBulletFullyTraceableToCitedFact()
    {
        var fact = MakeFact("Stage .NET MAUI", "Développement d'une application C# avec Azure DevOps");
        var content = new TailoredCvContent
        {
            Sections =
            [
                new TailoredSection
                {
                    SectionTitle = "Expériences",
                    Bullets = [new TailoredBullet { Text = "Développement C# avec Azure DevOps", SourceFactIds = [fact.Id] }]
                }
            ]
        };

        var issues = FactTraceabilityValidator.Validate(content, [fact]);

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_FlagsError_WhenBulletCitesNoSourceFact()
    {
        var content = new TailoredCvContent
        {
            Sections =
            [
                new TailoredSection
                {
                    SectionTitle = "Expériences",
                    Bullets = [new TailoredBullet { Text = "A dirigé une équipe de 12 personnes", SourceFactIds = [] }]
                }
            ]
        };

        var issues = FactTraceabilityValidator.Validate(content, []);

        Assert.Contains(issues, i => i.Severity == TraceabilityIssueSeverity.Error);
    }

    [Fact]
    public void Validate_FlagsError_WhenBulletCitesUnknownFactId()
    {
        var content = new TailoredCvContent
        {
            Sections =
            [
                new TailoredSection
                {
                    SectionTitle = "Expériences",
                    Bullets = [new TailoredBullet { Text = "Texte", SourceFactIds = [Guid.NewGuid()] }]
                }
            ]
        };

        var issues = FactTraceabilityValidator.Validate(content, []);

        Assert.Contains(issues, i => i.Severity == TraceabilityIssueSeverity.Error && i.Message.Contains("inconnu"));
    }

    [Fact]
    public void Validate_FlagsWarning_WhenNumberInBulletAbsentFromSourceFact()
    {
        var fact = MakeFact("Stage", "Développement d'une application mobile");
        var content = new TailoredCvContent
        {
            Sections =
            [
                new TailoredSection
                {
                    SectionTitle = "Expériences",
                    Bullets = [new TailoredBullet { Text = "A géré une équipe de 50 personnes", SourceFactIds = [fact.Id] }]
                }
            ]
        };

        var issues = FactTraceabilityValidator.Validate(content, [fact]);

        Assert.Contains(issues, i => i.Severity == TraceabilityIssueSeverity.Warning && i.Message.Contains("50"));
    }

    [Fact]
    public void Validate_DoesNotFlagOrdinaryCapitalizedWords()
    {
        var fact = MakeFact("Stage", "Développement d'une application mobile de suivi");
        var content = new TailoredCvContent
        {
            Sections =
            [
                new TailoredSection
                {
                    SectionTitle = "Expériences",
                    Bullets = [new TailoredBullet { Text = "Conception et développement d'une application de suivi", SourceFactIds = [fact.Id] }]
                }
            ]
        };

        var issues = FactTraceabilityValidator.Validate(content, [fact]);

        Assert.Empty(issues);
    }
}

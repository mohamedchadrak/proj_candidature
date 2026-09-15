using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Domain.Entities;

namespace PfeCopilot.Application.Applications;

public record FactMatch(CvFact Fact, double Relevance);

public record MatchResult(double ScorePercent, IReadOnlyList<FactMatch> RankedFacts, IReadOnlyList<string> UnmatchedKeywords);

/// <summary>
/// Calcule le recouvrement entre les faits du CV d'un utilisateur et les exigences d'une offre,
/// pour prioriser les faits pertinents dans la génération et donner un score de pertinence.
/// </summary>
public static class MatchingEngine
{
    public static MatchResult Score(IReadOnlyCollection<CvFact> facts, JobRequirements requirements)
    {
        var keywords = requirements.HardSkills
            .Concat(requirements.SoftSkills)
            .Concat(requirements.AtsKeywords)
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keywords.Count == 0 || facts.Count == 0)
        {
            return new MatchResult(0, [], keywords);
        }

        var matchedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rankedFacts = new List<FactMatch>();

        foreach (var fact in facts)
        {
            var haystack = $"{fact.Title} {fact.Organization} {fact.Description} {fact.Tags}";
            var hits = keywords.Count(keyword =>
            {
                var found = haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                if (found)
                {
                    matchedKeywords.Add(keyword);
                }
                return found;
            });

            if (hits > 0)
            {
                rankedFacts.Add(new FactMatch(fact, (double)hits / keywords.Count));
            }
        }

        rankedFacts = rankedFacts.OrderByDescending(m => m.Relevance).ToList();
        var unmatched = keywords.Where(k => !matchedKeywords.Contains(k)).ToList();
        var score = 100.0 * matchedKeywords.Count / keywords.Count;

        return new MatchResult(Math.Round(score, 1), rankedFacts, unmatched);
    }
}

using PfeCopilot.Application.Ai.Models;

namespace PfeCopilot.Application.Applications;

public record AtsFinding(string Message, bool IsBlocking);

public record AtsScoreResult(double ScorePercent, IReadOnlyList<AtsFinding> Findings);

/// <summary>
/// Évalue la compatibilité ATS (Applicant Tracking System) d'un CV généré : présence des mots-clés
/// de l'offre + absence des pièges de mise en page connus pour casser l'extraction de texte des ATS.
/// </summary>
public static class AtsScorer
{
    private static readonly string[] RiskyLatexConstructs =
    [
        "\\begin{tabular}",
        "\\includegraphics",
        "\\begin{multicols}",
        "\\twocolumn"
    ];

    public static AtsScoreResult Score(string latexSource, string plainTextContent, JobRequirements requirements)
    {
        var findings = new List<AtsFinding>();

        foreach (var construct in RiskyLatexConstructs)
        {
            if (latexSource.Contains(construct, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new AtsFinding(
                    $"Le CV utilise \"{construct}\" : certains ATS lisent mal les tableaux/colonnes/images, le contenu peut être perdu à l'extraction.",
                    IsBlocking: true));
            }
        }

        var keywords = requirements.AtsKeywords
            .Concat(requirements.HardSkills)
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missing = keywords
            .Where(k => !plainTextContent.Contains(k, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var keyword in missing)
        {
            findings.Add(new AtsFinding($"Mot-clé de l'offre absent du CV : \"{keyword}\".", IsBlocking: false));
        }

        double keywordScore = keywords.Count == 0 ? 100 : 100.0 * (keywords.Count - missing.Count) / keywords.Count;
        double layoutPenalty = findings.Count(f => f.IsBlocking) * 15;
        double finalScore = Math.Clamp(keywordScore - layoutPenalty, 0, 100);

        return new AtsScoreResult(Math.Round(finalScore, 1), findings);
    }
}

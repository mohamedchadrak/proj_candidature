using System.Text.RegularExpressions;
using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Domain.Entities;

namespace PfeCopilot.Application.Applications;

public enum TraceabilityIssueSeverity
{
    /// <summary>Le texte cite un CvFact qui n'existe pas / n'appartient pas à l'utilisateur : à bloquer.</summary>
    Error,
    /// <summary>Un chiffre ou un terme technique du texte généré n'est retrouvé dans aucun fait source cité : à faire relire.</summary>
    Warning
}

public record TraceabilityIssue(TraceabilityIssueSeverity Severity, string Location, string Message);

/// <summary>
/// Garde-fou anti-hallucination : vérifie que chaque phrase générée par l'IA reste
/// traçable aux PfeCopilot.Domain.Entities.CvFact réels de l'utilisateur. Ne bloque pas
/// la génération mais remonte des <see cref="TraceabilityIssue"/> pour relecture avant export.
/// </summary>
public static class FactTraceabilityValidator
{
    private static readonly Regex NumericTokenPattern = new(@"\b\d[\d.,]*\s?%?\b", RegexOptions.Compiled);

    // Ne cible que les tokens "à risque d'invention" : contiennent un chiffre, un symbole technique
    // (.NET, C#, CI/CD), ou sont des acronymes tout en majuscules (API, ATS) — pas les mots capitalisés
    // ordinaires de début de phrase, qui généreraient trop de faux positifs.
    private static readonly Regex TechnicalTokenPattern = new(@"\b[A-Za-z][A-Za-z0-9+#./-]*\d[A-Za-z0-9+#./-]*\b|\b[A-Z]{2,}\b|\b[A-Za-z]+[+#]\b|\.[A-Z][A-Za-z]+\b", RegexOptions.Compiled);

    public static IReadOnlyList<TraceabilityIssue> Validate(TailoredCvContent content, IReadOnlyCollection<CvFact> availableFacts)
    {
        var issues = new List<TraceabilityIssue>();
        var factsById = availableFacts.ToDictionary(f => f.Id);

        ValidateClaim("Résumé professionnel", content.ProfessionalSummary, content.SummarySourceFactIds, factsById, issues);

        foreach (var section in content.Sections)
        {
            foreach (var bullet in section.Bullets)
            {
                ValidateClaim($"{section.SectionTitle} — \"{Truncate(bullet.Text)}\"", bullet.Text, bullet.SourceFactIds, factsById, issues);
            }
        }

        return issues;
    }

    private static void ValidateClaim(
        string location,
        string generatedText,
        List<Guid> sourceFactIds,
        Dictionary<Guid, CvFact> factsById,
        List<TraceabilityIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(generatedText))
        {
            return;
        }

        if (sourceFactIds.Count == 0)
        {
            issues.Add(new TraceabilityIssue(TraceabilityIssueSeverity.Error, location,
                "Aucun fait source cité pour ce contenu généré : impossible de vérifier qu'il ne contient pas d'invention."));
            return;
        }

        var sourceTexts = new List<string>();
        foreach (var factId in sourceFactIds)
        {
            if (!factsById.TryGetValue(factId, out var fact))
            {
                issues.Add(new TraceabilityIssue(TraceabilityIssueSeverity.Error, location,
                    $"Référence à un fait source inconnu ({factId}) — n'appartient pas au CV de l'utilisateur."));
                continue;
            }

            sourceTexts.Add($"{fact.Title} {fact.Organization} {fact.Description} {fact.Tags}");
        }

        var combinedSource = string.Join(" \n ", sourceTexts);

        foreach (var token in ExtractClaimTokens(generatedText))
        {
            if (!combinedSource.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new TraceabilityIssue(TraceabilityIssueSeverity.Warning, location,
                    $"Le terme \"{token}\" apparaît dans le texte généré mais pas dans les faits sources cités — à vérifier avant envoi."));
            }
        }
    }

    private static IEnumerable<string> ExtractClaimTokens(string text)
    {
        foreach (Match match in NumericTokenPattern.Matches(text))
        {
            yield return match.Value.Trim();
        }

        foreach (Match match in TechnicalTokenPattern.Matches(text))
        {
            yield return match.Value.Trim();
        }
    }

    private static string Truncate(string text, int maxLength = 40)
        => text.Length <= maxLength ? text : text[..maxLength] + "…";
}

using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Application.Ai;

/// <summary>Abstraction sur le fournisseur IA (Claude, puis OpenAI), pour rester interchangeable via la config.</summary>
public interface IAiProvider
{
    AiProviderType ProviderType { get; }

    /// <summary>
    /// Vérifie qu'une clé API est acceptée par le fournisseur (appel léger, sans coût d'inférence,
    /// ex. liste des modèles disponibles) — utilisé à l'enregistrement d'une clé pour éviter de
    /// découvrir une faute de frappe seulement au moment d'une vraie génération.
    /// </summary>
    Task<bool> ValidateApiKeyAsync(string apiKeyPlainText, CancellationToken cancellationToken = default);

    Task<JobRequirements> ExtractRequirementsAsync(string offerText, string apiKeyPlainText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Structure fidèlement un CV existant (texte extrait d'un PDF) en faits atomiques, sans
    /// compléter ni déduire d'informations absentes du texte — à la différence de la génération
    /// adaptée, qui elle sélectionne/reformule des faits déjà validés.
    /// </summary>
    Task<ExtractedCvContent> ExtractCvFactsAsync(string cvText, string apiKeyPlainText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère un CV adapté à partir UNIQUEMENT des faits fournis. Le prompt système interdit
    /// explicitement toute invention : chaque bullet doit citer les CvFact dont elle est issue.
    /// </summary>
    Task<TailoredCvContent> GenerateTailoredCvAsync(
        IReadOnlyCollection<CvFact> facts,
        JobRequirements requirements,
        CandidateProfile profile,
        string apiKeyPlainText,
        CancellationToken cancellationToken = default);

    Task<string> GenerateCoverLetterAsync(
        IReadOnlyCollection<CvFact> facts,
        JobRequirements requirements,
        CandidateProfile profile,
        string offerText,
        string apiKeyPlainText,
        CancellationToken cancellationToken = default);
}

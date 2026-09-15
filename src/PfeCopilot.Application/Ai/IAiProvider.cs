using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Application.Ai;

/// <summary>Abstraction sur le fournisseur IA (Claude, puis OpenAI), pour rester interchangeable via la config.</summary>
public interface IAiProvider
{
    AiProviderType ProviderType { get; }

    Task<JobRequirements> ExtractRequirementsAsync(string offerText, string apiKeyPlainText, CancellationToken cancellationToken = default);

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

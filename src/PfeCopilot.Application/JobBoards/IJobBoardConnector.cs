using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Application.JobBoards;

public record ExternalJobOffer(
    string ExternalId,
    string Title,
    string? Company,
    string? Location,
    string RawDescription,
    string? Url,
    DateTime? PostedAtUtc);

/// <summary>Connecteur vers une API officielle d'offres d'emploi (pas de scraping de sites tiers).</summary>
public interface IJobBoardConnector
{
    JobOfferSource Source { get; }

    Task<IReadOnlyList<ExternalJobOffer>> SearchAsync(SavedSearch search, CancellationToken cancellationToken = default);
}

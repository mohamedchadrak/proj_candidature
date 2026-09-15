using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Domain.Entities;

public class JobOffer : IUserOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }

    public required JobOfferSource Source { get; set; }
    /// <summary>Identifiant de l'offre chez la source, utilisé pour le dédoublonnage. Null pour un import manuel/collé.</summary>
    public string? ExternalId { get; set; }

    public required string Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public required string RawDescription { get; set; }
    public string? Url { get; set; }

    public DateTime? PostedAtUtc { get; set; }
    public DateTime FetchedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Score de correspondance CV/offre calculé par MatchingEngine, de 0 à 100.</summary>
    public double MatchScore { get; set; }

    public Guid? SavedSearchId { get; set; }
}

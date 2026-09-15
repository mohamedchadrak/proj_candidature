using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Domain.Entities;

public class CandidateProfile : IUserOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }

    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Address { get; set; }

    /// <summary>Angle de ciblage des candidatures, ex. "Intelligence Artificielle / Machine Learning".</summary>
    public string TargetedSpecialty { get; set; } = "Intelligence Artificielle / Machine Learning";

    /// <summary>Mots-clés par défaut utilisés pour la veille emploi, séparés par des virgules.</summary>
    public string DefaultWatchKeywords { get; set; } = "intelligence artificielle, machine learning, data science, stage PFE";

    /// <summary>Fournisseur IA utilisé par défaut pour la génération, si l'utilisateur a configuré plusieurs clés actives.</summary>
    public AiProviderType PreferredAiProvider { get; set; } = AiProviderType.Claude;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Domain.Entities;

/// <summary>
/// Fait atomique et vérifiable extrait du CV réel de l'utilisateur. C'est la seule
/// matière première que l'IA a le droit d'utiliser pour générer un CV/LM adapté :
/// elle peut reformuler, sélectionner ou réordonner ces faits, jamais en inventer.
/// </summary>
public class CvFact : IUserOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }

    public required CvFactType Type { get; set; }
    public required string Title { get; set; }
    public string? Organization { get; set; }
    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Tags libres (ex. "C#", ".NET MAUI", "Azure") utilisés par le matching et la traçabilité.</summary>
    public string Tags { get; set; } = string.Empty;

    /// <summary>Ordre d'affichage préféré au sein de son <see cref="Type"/>.</summary>
    public int DisplayOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<string> TagList =>
        Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

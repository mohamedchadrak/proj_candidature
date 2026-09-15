namespace PfeCopilot.Application.Ai.Models;

/// <summary>
/// CV adapté généré par l'IA. Chaque section référence les <see cref="CvFactSourceIds"/> des
/// PfeCopilot.Domain.Entities.CvFact dont elle est issue, pour la traçabilité anti-hallucination.
/// </summary>
public class TailoredCvContent
{
    public string ProfessionalSummary { get; set; } = string.Empty;
    public List<Guid> SummarySourceFactIds { get; set; } = [];

    public List<TailoredSection> Sections { get; set; } = [];
}

public class TailoredSection
{
    public string SectionTitle { get; set; } = string.Empty;
    public List<TailoredBullet> Bullets { get; set; } = [];
}

public class TailoredBullet
{
    public required string Text { get; set; }
    /// <summary>Id(s) des CvFact source(s) dont ce texte est une reformulation/sélection.</summary>
    public List<Guid> SourceFactIds { get; set; } = [];
}

using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Domain.Entities;

/// <summary>Une candidature générée pour une offre donnée : CV adapté, lettre de motivation, LaTeX, score ATS.</summary>
public class JobApplication : IUserOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }

    public Guid? JobOfferId { get; set; }
    public JobOffer? JobOffer { get; set; }

    /// <summary>CV adapté, sérialisé en JSON (structure TailoredCvContent), traçable vers les CvFact sources.</summary>
    public required string TailoredCvJson { get; set; }
    public required string CoverLetterText { get; set; }
    public required string LatexSource { get; set; }

    public double AtsScore { get; set; }
    public string AtsSuggestionsJson { get; set; } = "[]";

    public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Brouillon;
    public DateTime? FollowUpReminderAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

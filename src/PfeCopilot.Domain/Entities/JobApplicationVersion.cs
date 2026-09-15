namespace PfeCopilot.Domain.Entities;

/// <summary>Instantané d'une <see cref="JobApplication"/> à un instant donné, pour permettre historique/diff.</summary>
public class JobApplicationVersion : IUserOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }

    public required Guid JobApplicationId { get; set; }
    public required string LatexSource { get; set; }
    public required string CoverLetterText { get; set; }
    public string? EditSummary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

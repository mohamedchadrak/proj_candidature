namespace PfeCopilot.Domain.Entities;

public class SavedSearch : IUserOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }

    public required string Label { get; set; }
    /// <summary>Mots-clés séparés par des virgules, ex. "intelligence artificielle, machine learning".</summary>
    public required string Keywords { get; set; }
    public string? Location { get; set; }
    public string ContractType { get; set; } = "Stage";

    public bool IsActive { get; set; } = true;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromHours(4);
    public DateTime? LastRunAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<string> KeywordList =>
        Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

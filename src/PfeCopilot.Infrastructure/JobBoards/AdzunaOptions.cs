namespace PfeCopilot.Infrastructure.JobBoards;

/// <summary>Identifiants Adzuna (gratuits sur https://developer.adzuna.com) — configuration plateforme.</summary>
public class AdzunaOptions
{
    public const string SectionName = "Adzuna";

    public string AppId { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string Country { get; set; } = "fr";
    public string ApiBaseUrl { get; set; } = "https://api.adzuna.com/v1/api/jobs/";
}

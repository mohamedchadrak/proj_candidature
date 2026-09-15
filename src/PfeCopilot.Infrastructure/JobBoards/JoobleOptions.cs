namespace PfeCopilot.Infrastructure.JobBoards;

/// <summary>
/// Identifiants Jooble (clé API gratuite sur demande via https://jooble.org/api/about) —
/// configuration plateforme. Jooble est un agrégateur légal d'offres provenant de nombreux
/// sites (y compris, indirectement, certaines offres relayées par de grands jobboards),
/// contrairement au scraping direct de LinkedIn/Indeed qui violerait leurs CGU.
/// </summary>
public class JoobleOptions
{
    public const string SectionName = "Jooble";

    public string ApiKey { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://jooble.org/api/";
}

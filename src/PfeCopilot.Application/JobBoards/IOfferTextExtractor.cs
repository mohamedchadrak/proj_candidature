namespace PfeCopilot.Application.JobBoards;

/// <summary>Récupère et nettoie le texte d'une offre d'emploi à partir de son URL publique.</summary>
public interface IOfferTextExtractor
{
    Task<string> ExtractFromUrlAsync(string url, CancellationToken cancellationToken = default);
}

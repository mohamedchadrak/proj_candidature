using Microsoft.EntityFrameworkCore;
using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Application.Applications;
using PfeCopilot.Application.Common;
using PfeCopilot.Application.JobBoards;
using PfeCopilot.Domain.Entities;

namespace PfeCopilot.Worker;

/// <summary>
/// Veille emploi multi-tenant : parcourt périodiquement les <see cref="SavedSearch"/> de TOUS les
/// utilisateurs (traitement système, pas de "current user" HTTP) via les connecteurs officiels
/// (France Travail, Adzuna), et alimente le tableau de bord "Offres" de chaque compte.
/// Contrairement au pipeline de génération CV/LM, cette veille ne consomme aucun appel IA : le score
/// de pertinence est calculé par recouvrement de mots-clés (MatchingEngine) pour rester gratuite et
/// tourner sans clé API utilisateur.
/// </summary>
public class JobWatchBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<JobWatchBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);
    private const double NotificationThresholdPercent = 30.0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            try
            {
                await RunDueSearchesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur durant le cycle de veille emploi.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunDueSearchesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var connectors = scope.ServiceProvider.GetRequiredService<IEnumerable<IJobBoardConnector>>();

        var now = DateTime.UtcNow;
        var dueSearches = await db.SavedSearches
            .IgnoreQueryFilters()
            .Where(s => s.IsActive)
            .ToListAsync(cancellationToken);
        dueSearches = dueSearches
            .Where(s => s.LastRunAtUtc is null || now - s.LastRunAtUtc >= s.PollingInterval)
            .ToList();

        if (dueSearches.Count == 0)
        {
            return;
        }

        logger.LogInformation("Veille emploi : {Count} recherche(s) à exécuter.", dueSearches.Count);

        foreach (var search in dueSearches)
        {
            await RunSearchAsync(db, connectors, search, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RunSearchAsync(IAppDbContext db, IEnumerable<IJobBoardConnector> connectors, SavedSearch search, CancellationToken cancellationToken)
    {
        var userFacts = await db.CvFacts.IgnoreQueryFilters()
            .Where(f => f.UserId == search.UserId)
            .ToListAsync(cancellationToken);

        var syntheticRequirements = new JobRequirements { HardSkills = search.KeywordList.ToList() };

        foreach (var connector in connectors)
        {
            IReadOnlyList<ExternalJobOffer> results;
            try
            {
                results = await connector.SearchAsync(search, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Connecteur {Source} en échec pour la recherche \"{Label}\".", connector.Source, search.Label);
                continue;
            }

            foreach (var external in results)
            {
                var existing = await db.JobOffers.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(o => o.UserId == search.UserId
                        && o.Source == connector.Source
                        && o.ExternalId == external.ExternalId, cancellationToken);

                if (existing is not null)
                {
                    continue; // déjà connue, pas de mise à jour du score pour ne pas écraser un score déjà revu.
                }

                var matchScore = ComputeMatchScore(userFacts, syntheticRequirements, external.RawDescription, search);

                var offer = new JobOffer
                {
                    UserId = search.UserId,
                    Source = connector.Source,
                    ExternalId = external.ExternalId,
                    Title = external.Title,
                    Company = external.Company,
                    Location = external.Location,
                    RawDescription = external.RawDescription,
                    Url = external.Url,
                    PostedAtUtc = external.PostedAtUtc,
                    MatchScore = matchScore,
                    SavedSearchId = search.Id
                };

                db.JobOffers.Add(offer);

                if (matchScore >= NotificationThresholdPercent)
                {
                    // Point d'extension : brancher ici un envoi d'email/digest. Pour le MVP, l'offre
                    // est simplement visible avec son score dans le tableau de bord "Offres".
                    logger.LogInformation(
                        "Nouvelle offre pertinente ({Score}%) pour {UserId} : {Title} — {Company}",
                        matchScore, search.UserId, offer.Title, offer.Company);
                }
            }
        }

        search.LastRunAtUtc = DateTime.UtcNow;
    }

    private static double ComputeMatchScore(
        IReadOnlyCollection<CvFact> userFacts,
        JobRequirements syntheticRequirements,
        string offerDescription,
        SavedSearch search)
    {
        // Le CV ne mentionne pas nécessairement les mots de l'offre : on complète le score de
        // recouvrement CV/mots-clés par une vérification que l'offre elle-même correspond aux
        // mots-clés de la recherche (utile quand elle a été retournée avec un intitulé large).
        var cvMatch = MatchingEngine.Score(userFacts, syntheticRequirements);
        var offerKeywordHits = search.KeywordList.Count(k => offerDescription.Contains(k, StringComparison.OrdinalIgnoreCase));
        var offerRelevance = search.KeywordList.Count == 0 ? 0 : 100.0 * offerKeywordHits / search.KeywordList.Count;

        return Math.Round((cvMatch.ScorePercent + offerRelevance) / 2, 1);
    }
}

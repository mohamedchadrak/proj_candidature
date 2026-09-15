using Microsoft.EntityFrameworkCore;
using PfeCopilot.Application.Ai;
using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Application.Common;
using PfeCopilot.Application.CvImport;
using PfeCopilot.Application.Security;

namespace PfeCopilot.Application.Applications;

/// <summary>
/// Extrait fidèlement le contenu d'un CV PDF existant en faits structurés, proposés à
/// l'utilisateur pour relecture avant d'être enregistrés comme PfeCopilot.Domain.Entities.CvFact
/// (voir la page "Mon CV source"). N'écrit rien en base : seule la persistance après validation
/// par l'utilisateur (bouton "Valider l'import") crée les CvFact.
/// </summary>
public class ImportCvFromPdfHandler(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IEnumerable<IAiProvider> aiProviders,
    IApiKeyProtector apiKeyProtector,
    IPdfTextExtractor pdfTextExtractor)
{
    public async Task<ExtractedCvContent> HandleAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("Aucun utilisateur authentifié.");

        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profil candidat introuvable.");

        var activeKeys = await db.ApiKeyConfigs
            .Where(k => k.UserId == userId && k.IsActive)
            .ToListAsync(cancellationToken);

        var (aiProvider, activeKey) = AiProviderSelector.SelectActiveProvider(aiProviders.ToList(), activeKeys, profile.PreferredAiProvider);
        var plainTextKey = apiKeyProtector.Unprotect(activeKey.CipherText);

        var cvText = await pdfTextExtractor.ExtractTextAsync(pdfStream, cancellationToken);
        if (string.IsNullOrWhiteSpace(cvText))
        {
            throw new InvalidOperationException(
                "Aucun texte n'a pu être extrait de ce PDF — c'est probablement un CV scanné (image) sans OCR, qu'il faut alors saisir manuellement.");
        }

        var extracted = await aiProvider.ExtractCvFactsAsync(cvText, plainTextKey, cancellationToken);

        activeKey.LastUsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return extracted;
    }
}

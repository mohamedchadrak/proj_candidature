using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PfeCopilot.Application.Ai;
using PfeCopilot.Application.Common;
using PfeCopilot.Application.Latex;
using PfeCopilot.Application.Security;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Application.Applications;

public record GenerateApplicationRequest(string OfferText, Guid? JobOfferId, string? CompanyName, string? JobTitle);

public record GenerateApplicationResult(
    JobApplication Application,
    IReadOnlyList<TraceabilityIssue> TraceabilityIssues,
    AtsScoreResult AtsResult,
    MatchResult MatchResult);

/// <summary>
/// Orchestre le pipeline complet : extraction des exigences de l'offre, matching avec les faits
/// du CV de l'utilisateur courant, génération IA du CV/LM adaptés, validation anti-hallucination,
/// rendu LaTeX puis persistance de la candidature.
/// </summary>
public class GenerateApplicationHandler(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IEnumerable<IAiProvider> aiProviders,
    IApiKeyProtector apiKeyProtector,
    ILatexTemplateRenderer latexRenderer)
{
    public async Task<GenerateApplicationResult> HandleAsync(GenerateApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("Aucun utilisateur authentifié.");

        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profil candidat introuvable — complétez d'abord votre CV source.");

        var facts = await db.CvFacts.Where(f => f.UserId == userId).ToListAsync(cancellationToken);
        if (facts.Count == 0)
        {
            throw new InvalidOperationException("Aucun fait de CV enregistré — importez d'abord votre CV source.");
        }

        var activeKeys = await db.ApiKeyConfigs
            .Where(k => k.UserId == userId && k.IsActive)
            .ToListAsync(cancellationToken);

        var (aiProvider, activeKey) = AiProviderSelector.SelectActiveProvider(aiProviders.ToList(), activeKeys, profile.PreferredAiProvider);

        var plainTextKey = apiKeyProtector.Unprotect(activeKey.CipherText);

        var requirements = await aiProvider.ExtractRequirementsAsync(request.OfferText, plainTextKey, cancellationToken);
        var matchResult = MatchingEngine.Score(facts, requirements);

        var tailoredCv = await aiProvider.GenerateTailoredCvAsync(facts, requirements, profile, plainTextKey, cancellationToken);
        var coverLetterText = await aiProvider.GenerateCoverLetterAsync(facts, requirements, profile, request.OfferText, plainTextKey, cancellationToken);

        var traceabilityIssues = FactTraceabilityValidator.Validate(tailoredCv, facts);

        var cvLatex = latexRenderer.RenderCv(tailoredCv, profile, facts);
        var coverLetterLatex = latexRenderer.RenderCoverLetter(coverLetterText, profile, request.CompanyName ?? requirements.CompanyContext, request.JobTitle ?? requirements.JobTitle);
        var combinedLatex = string.Join("\n\n%% ==== LETTRE DE MOTIVATION ====\n\n", cvLatex, coverLetterLatex);

        var plainTextForAts = string.Join(" ", tailoredCv.Sections.SelectMany(s => s.Bullets.Select(b => b.Text)).Append(tailoredCv.ProfessionalSummary).Append(coverLetterText));
        var atsResult = AtsScorer.Score(combinedLatex, plainTextForAts, requirements);

        var application = new JobApplication
        {
            UserId = userId,
            JobOfferId = request.JobOfferId,
            TailoredCvJson = JsonSerializer.Serialize(tailoredCv),
            CoverLetterText = coverLetterText,
            LatexSource = combinedLatex,
            AtsScore = atsResult.ScorePercent,
            AtsSuggestionsJson = JsonSerializer.Serialize(atsResult.Findings),
            Status = JobApplicationStatus.Brouillon
        };

        db.JobApplications.Add(application);
        activeKey.LastUsedAtUtc = DateTime.UtcNow;

        db.JobApplicationVersions.Add(new JobApplicationVersion
        {
            UserId = userId,
            JobApplicationId = application.Id,
            LatexSource = combinedLatex,
            CoverLetterText = coverLetterText,
            EditSummary = "Génération initiale"
        });

        await db.SaveChangesAsync(cancellationToken);

        return new GenerateApplicationResult(application, traceabilityIssues, atsResult, matchResult);
    }
}

using System.Text.Json;
using PfeCopilot.Application.Ai;
using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Infrastructure.Ai;

/// <summary>
/// Factorise les prompts et le garde-fou anti-hallucination communs à tous les fournisseurs IA :
/// seule la manière d'appeler l'API (<see cref="SendRawAsync"/>) change d'un fournisseur à l'autre.
/// Centraliser ces prompts ici évite que Claude et Gemini divergent sur la règle "jamais inventer".
/// </summary>
public abstract class AiProviderBase : IAiProvider
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public abstract AiProviderType ProviderType { get; }

    /// <summary>Envoie un message système + utilisateur au fournisseur et retourne le texte brut de la réponse.</summary>
    protected abstract Task<string> SendRawAsync(string system, string userMessage, string apiKey, CancellationToken cancellationToken);

    public abstract Task<bool> ValidateApiKeyAsync(string apiKeyPlainText, CancellationToken cancellationToken = default);

    public async Task<JobRequirements> ExtractRequirementsAsync(string offerText, string apiKeyPlainText, CancellationToken cancellationToken = default)
    {
        const string system = """
            Tu es un assistant de recrutement. À partir du texte brut d'une offre d'emploi, extrait les
            exigences clés. Réponds STRICTEMENT avec un objet JSON conforme au schéma suivant, sans texte
            autour ni bloc markdown :
            {
              "jobTitle": string, "seniorityLevel": string,
              "hardSkills": string[], "softSkills": string[], "atsKeywords": string[],
              "companyContext": string
            }
            """;

        var json = await SendAndExtractJsonAsync(system, offerText, apiKeyPlainText, cancellationToken);
        return DeserializeOrThrow<JobRequirements>(json);
    }

    public async Task<ExtractedCvContent> ExtractCvFactsAsync(string cvText, string apiKeyPlainText, CancellationToken cancellationToken = default)
    {
        const string system = """
            Tu structures un CV existant en faits atomiques, sans jamais ajouter, déduire ou enjoliver
            une information absente du texte fourni. Règle ABSOLUE : retranscris fidèlement, n'invente
            rien, ne complète pas une date ou une compétence non explicitement mentionnée — si une
            information n'est pas dans le texte, laisse le champ correspondant vide/null.

            Découpe le contenu en faits, chacun rattaché à l'une de ces catégories exactement :
            Experience, Formation, Projet, CompetenceTechnique, CompetenceTransversale, Langue, CentreInteret.
            Pour les dates, utilise le format "yyyy-MM" si le mois est connu, "yyyy" sinon, ou null si absent.

            Réponds STRICTEMENT avec un objet JSON conforme au schéma suivant, sans texte autour ni
            bloc markdown :
            {
              "fullName": string|null, "phone": string|null, "email": string|null,
              "linkedInUrl": string|null, "websiteUrl": string|null,
              "facts": [ { "type": string, "title": string, "organization": string|null,
                           "description": string|null, "startDate": string|null, "endDate": string|null,
                           "tags": string } ]
            }
            """;

        var json = await SendAndExtractJsonAsync(system, cvText, apiKeyPlainText, cancellationToken);
        return DeserializeOrThrow<ExtractedCvContent>(json);
    }

    public async Task<TailoredCvContent> GenerateTailoredCvAsync(
        IReadOnlyCollection<CvFact> facts,
        JobRequirements requirements,
        CandidateProfile profile,
        string apiKeyPlainText,
        CancellationToken cancellationToken = default)
    {
        var system = $$"""
            Tu es un assistant de rédaction de CV. Tu dois adapter le CV d'un candidat à une offre
            d'emploi précise, en respectant une règle ABSOLUE et NON NÉGOCIABLE :
            tu ne peux QUE reformuler, sélectionner, réordonner ou mettre en avant des faits fournis
            ci-dessous (identifiés par un "id"). Tu ne dois JAMAIS inventer une compétence, une durée,
            un chiffre, un poste ou une réalisation absente de ces faits. Chaque phrase générée doit
            citer le ou les "id" des faits dont elle est directement issue dans "sourceFactIds".
            Si aucun fait ne permet de répondre à une exigence de l'offre, n'invente rien : omets-la.

            Spécialité ciblée par le candidat : {{profile.TargetedSpecialty}}.

            Réponds STRICTEMENT avec un objet JSON conforme au schéma suivant, sans texte autour ni
            bloc markdown :
            {
              "professionalSummary": string, "summarySourceFactIds": string[] (uuids),
              "sections": [ { "sectionTitle": string, "bullets": [ { "text": string, "sourceFactIds": string[] (uuids) } ] } ]
            }
            """;

        var userPrompt = BuildFactsAndRequirementsPrompt(facts, requirements);
        var json = await SendAndExtractJsonAsync(system, userPrompt, apiKeyPlainText, cancellationToken);
        return DeserializeOrThrow<TailoredCvContent>(json);
    }

    public async Task<string> GenerateCoverLetterAsync(
        IReadOnlyCollection<CvFact> facts,
        JobRequirements requirements,
        CandidateProfile profile,
        string offerText,
        string apiKeyPlainText,
        CancellationToken cancellationToken = default)
    {
        var system = $$"""
            Tu rédiges une lettre de motivation en français, professionnelle, concise (250-350 mots),
            pour le candidat décrit par les faits fournis ci-dessous, ciblant sa spécialité
            "{{profile.TargetedSpecialty}}". Règle ABSOLUE : tu ne peux t'appuyer QUE sur les faits
            fournis, tu ne dois JAMAIS inventer une expérience, une compétence ou un chiffre absent
            de ces faits. Réponds uniquement avec le texte brut de la lettre, sans JSON ni markdown.
            """;

        var userPrompt = BuildFactsAndRequirementsPrompt(facts, requirements) + $"\n\nTexte de l'offre :\n{offerText}";
        return await SendRawAsync(system, userPrompt, apiKeyPlainText, cancellationToken);
    }

    private static string BuildFactsAndRequirementsPrompt(IReadOnlyCollection<CvFact> facts, JobRequirements requirements)
    {
        var factsJson = JsonSerializer.Serialize(facts.Select(f => new
        {
            id = f.Id,
            type = f.Type.ToString(),
            title = f.Title,
            organization = f.Organization,
            description = f.Description,
            tags = f.TagList,
            period = $"{f.StartDate?.ToString("yyyy-MM")} - {f.EndDate?.ToString("yyyy-MM") ?? "présent"}"
        }), JsonOptions);

        var requirementsJson = JsonSerializer.Serialize(requirements, JsonOptions);

        return $"Faits du CV (seule source autorisée) :\n{factsJson}\n\nExigences de l'offre :\n{requirementsJson}";
    }

    private async Task<string> SendAndExtractJsonAsync(string system, string userMessage, string apiKey, CancellationToken cancellationToken)
    {
        var text = await SendRawAsync(system, userMessage, apiKey, cancellationToken);
        return ExtractJsonPayload(text);
    }

    /// <summary>Le modèle répond parfois avec du texte autour du JSON malgré la consigne : on isole l'objet JSON.</summary>
    private static string ExtractJsonPayload(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException("Réponse IA sans JSON exploitable.");
        }

        return text[start..(end + 1)];
    }

    private static T DeserializeOrThrow<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Impossible de désérialiser la réponse IA en {typeof(T).Name}.");
    }
}

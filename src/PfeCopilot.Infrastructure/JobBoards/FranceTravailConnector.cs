using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PfeCopilot.Application.JobBoards;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Infrastructure.JobBoards;

/// <summary>Connecteur vers l'API officielle et gratuite France Travail (ex Pôle emploi) "Offres d'emploi v2".</summary>
public class FranceTravailConnector(
    IHttpClientFactory httpClientFactory,
    IOptions<FranceTravailOptions> options,
    ILogger<FranceTravailConnector> logger) : IJobBoardConnector
{
    private readonly FranceTravailOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public JobOfferSource Source => JobOfferSource.FranceTravail;

    public async Task<IReadOnlyList<ExternalJobOffer>> SearchAsync(SavedSearch search, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            logger.LogInformation("France Travail non configuré (ClientId/ClientSecret manquants) — recherche ignorée.");
            return [];
        }

        var token = await GetAccessTokenAsync(cancellationToken);
        var client = httpClientFactory.CreateClient(nameof(FranceTravailConnector));
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var query = BuildQuery(search);
        using var response = await client.GetAsync($"offres/search?{query}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Recherche France Travail échouée ({Status}) pour \"{Label}\".", response.StatusCode, search.Label);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(JsonOptions, cancellationToken);
        if (payload?.Resultats is null)
        {
            return [];
        }

        return payload.Resultats.Select(o => new ExternalJobOffer(
            ExternalId: o.Id,
            Title: o.Intitule,
            Company: o.Entreprise?.Nom,
            Location: o.LieuTravail?.Libelle,
            RawDescription: o.Description ?? string.Empty,
            Url: o.OrigineOffre?.UrlOrigine,
            PostedAtUtc: o.DateCreation
        )).ToList();
    }

    private static string BuildQuery(SavedSearch search)
    {
        var parameters = new List<string>
        {
            $"motsCles={Uri.EscapeDataString(string.Join(',', search.KeywordList))}",
            "natureContrat=E1" // Stage
        };

        if (!string.IsNullOrWhiteSpace(search.Location))
        {
            parameters.Add($"commune={Uri.EscapeDataString(search.Location)}");
        }

        return string.Join('&', parameters);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            var client = httpClientFactory.CreateClient(nameof(FranceTravailConnector) + ".Auth");
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = _options.Scope
            });

            using var response = await client.PostAsync(_options.TokenUrl, form, cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Réponse OAuth2 France Travail vide.");

            _cachedToken = token.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 30);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private class SearchResponse
    {
        [JsonPropertyName("resultats")] public List<OffreDto>? Resultats { get; set; }
    }

    private class OffreDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("intitule")] public string Intitule { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("dateCreation")] public DateTime? DateCreation { get; set; }
        [JsonPropertyName("entreprise")] public EntrepriseDto? Entreprise { get; set; }
        [JsonPropertyName("lieuTravail")] public LieuTravailDto? LieuTravail { get; set; }
        [JsonPropertyName("origineOffre")] public OrigineOffreDto? OrigineOffre { get; set; }
    }

    private class EntrepriseDto
    {
        [JsonPropertyName("nom")] public string? Nom { get; set; }
    }

    private class LieuTravailDto
    {
        [JsonPropertyName("libelle")] public string? Libelle { get; set; }
    }

    private class OrigineOffreDto
    {
        [JsonPropertyName("urlOrigine")] public string? UrlOrigine { get; set; }
    }
}

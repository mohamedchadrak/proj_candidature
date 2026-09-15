using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PfeCopilot.Application.JobBoards;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Infrastructure.JobBoards;

/// <summary>
/// Connecteur vers l'API officielle Jooble (agrégateur d'offres, clé gratuite sur demande).
/// Élargit la couverture de la veille sans scraper directement de sites tiers.
/// </summary>
public class JoobleConnector(
    IHttpClientFactory httpClientFactory,
    IOptions<JoobleOptions> options,
    ILogger<JoobleConnector> logger) : IJobBoardConnector
{
    private readonly JoobleOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobOfferSource Source => JobOfferSource.Jooble;

    public async Task<IReadOnlyList<ExternalJobOffer>> SearchAsync(SavedSearch search, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogInformation("Jooble non configuré (ApiKey manquante) — recherche ignorée.");
            return [];
        }

        var client = httpClientFactory.CreateClient(nameof(JoobleConnector));
        client.BaseAddress = new Uri(_options.ApiBaseUrl);

        var requestBody = new JoobleRequest
        {
            Keywords = string.Join(' ', search.KeywordList.Append(search.ContractType)),
            Location = search.Location ?? string.Empty
        };

        using var response = await client.PostAsJsonAsync(_options.ApiKey, requestBody, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Recherche Jooble échouée ({Status}) pour \"{Label}\".", response.StatusCode, search.Label);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<JoobleResponse>(JsonOptions, cancellationToken);
        if (payload?.Jobs is null)
        {
            return [];
        }

        return payload.Jobs
            .Where(j => !string.IsNullOrWhiteSpace(j.Id) && !string.IsNullOrWhiteSpace(j.Link))
            .Select(j => new ExternalJobOffer(
                ExternalId: j.Id!,
                Title: j.Title ?? "(sans titre)",
                Company: j.Company,
                Location: j.Location,
                RawDescription: j.Snippet ?? string.Empty,
                Url: j.Link,
                PostedAtUtc: DateTime.TryParse(j.Updated, out var updated) ? updated : null
            ))
            .ToList();
    }

    private class JoobleRequest
    {
        [JsonPropertyName("keywords")] public string Keywords { get; set; } = string.Empty;
        [JsonPropertyName("location")] public string Location { get; set; } = string.Empty;
    }

    private class JoobleResponse
    {
        [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
        [JsonPropertyName("jobs")] public List<JoobleJobDto>? Jobs { get; set; }
    }

    private class JoobleJobDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("snippet")] public string? Snippet { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("updated")] public string? Updated { get; set; }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PfeCopilot.Application.JobBoards;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Infrastructure.JobBoards;

/// <summary>Connecteur vers l'API officielle et gratuite Adzuna.</summary>
public class AdzunaConnector(
    IHttpClientFactory httpClientFactory,
    IOptions<AdzunaOptions> options,
    ILogger<AdzunaConnector> logger) : IJobBoardConnector
{
    private readonly AdzunaOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobOfferSource Source => JobOfferSource.Adzuna;

    public async Task<IReadOnlyList<ExternalJobOffer>> SearchAsync(SavedSearch search, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AppId) || string.IsNullOrWhiteSpace(_options.AppKey))
        {
            logger.LogInformation("Adzuna non configuré (AppId/AppKey manquants) — recherche ignorée.");
            return [];
        }

        var client = httpClientFactory.CreateClient(nameof(AdzunaConnector));
        client.BaseAddress = new Uri(_options.ApiBaseUrl);

        var what = Uri.EscapeDataString(string.Join(' ', search.KeywordList));
        var where = string.IsNullOrWhiteSpace(search.Location) ? "" : $"&where={Uri.EscapeDataString(search.Location)}";
        var url = $"{_options.Country}/search/1?app_id={_options.AppId}&app_key={_options.AppKey}&what={what}{where}&results_per_page=25&content-type=application/json";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Recherche Adzuna échouée ({Status}) pour \"{Label}\".", response.StatusCode, search.Label);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(JsonOptions, cancellationToken);
        if (payload?.Results is null)
        {
            return [];
        }

        return payload.Results.Select(r => new ExternalJobOffer(
            ExternalId: r.Id,
            Title: r.Title,
            Company: r.Company?.DisplayName,
            Location: r.Location?.DisplayName,
            RawDescription: r.Description ?? string.Empty,
            Url: r.RedirectUrl,
            PostedAtUtc: r.Created
        )).ToList();
    }

    private class SearchResponse
    {
        [JsonPropertyName("results")] public List<ResultDto>? Results { get; set; }
    }

    private class ResultDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("redirect_url")] public string? RedirectUrl { get; set; }
        [JsonPropertyName("created")] public DateTime? Created { get; set; }
        [JsonPropertyName("company")] public CompanyDto? Company { get; set; }
        [JsonPropertyName("location")] public LocationDto? Location { get; set; }
    }

    private class CompanyDto
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    }

    private class LocationDto
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Infrastructure.Ai;

/// <summary>
/// Fournisseur IA : Anthropic Claude, appelé via l'API Messages avec la clé personnelle de
/// l'utilisateur (modèle "bring your own key" — jamais la clé d'un autre compte).
/// </summary>
public class ClaudeAiProvider(IHttpClientFactory httpClientFactory, IOptions<AnthropicOptions> options) : AiProviderBase
{
    private readonly AnthropicOptions _options = options.Value;

    public override AiProviderType ProviderType => AiProviderType.Claude;

    public override async Task<bool> ValidateApiKeyAsync(string apiKeyPlainText, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient(apiKeyPlainText);
        using var response = await client.GetAsync("v1/models", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    protected override async Task<string> SendRawAsync(string system, string userMessage, string apiKey, CancellationToken cancellationToken)
    {
        var client = CreateAuthenticatedClient(apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var request = new AnthropicRequest
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = system,
            Messages = [new AnthropicMessage { Role = "user", Content = userMessage }]
        };

        using var httpResponse = await client.PostAsJsonAsync("v1/messages", request, JsonOptions, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            string? errorMessage = null;
            try
            {
                var errorBody = await httpResponse.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOptions, cancellationToken);
                errorMessage = errorBody?.Error?.Message;
            }
            catch (JsonException)
            {
                // Une panne en amont (proxy, passerelle) peut renvoyer du texte/HTML au lieu de JSON.
            }

            var message = $"Erreur API Anthropic ({httpResponse.StatusCode}) : {errorMessage ?? "inconnue"}";
            if (IsTransientStatus(httpResponse.StatusCode))
            {
                throw new AiProviderTransientException(message);
            }
            throw new InvalidOperationException(message);
        }

        var body = await httpResponse.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse vide de l'API Anthropic.");

        return body.Content.FirstOrDefault(c => c.Type == "text")?.Text
            ?? throw new InvalidOperationException("Aucun contenu texte dans la réponse Anthropic.");
    }

    private HttpClient CreateAuthenticatedClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient(nameof(ClaudeAiProvider));
        client.BaseAddress = new Uri(_options.BaseUrl);
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Remove("anthropic-version");
        client.DefaultRequestHeaders.Add("anthropic-version", _options.ApiVersion);
        return client;
    }
}

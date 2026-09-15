using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Infrastructure.Ai;

/// <summary>
/// Fournisseur IA alternatif : Google Gemini, appelé via l'API Generative Language avec la clé
/// personnelle de l'utilisateur (modèle "bring your own key", comme pour Claude).
/// </summary>
public class GeminiAiProvider(IHttpClientFactory httpClientFactory, IOptions<GeminiOptions> options) : AiProviderBase
{
    private readonly GeminiOptions _options = options.Value;

    public override AiProviderType ProviderType => AiProviderType.Gemini;

    public override async Task<bool> ValidateApiKeyAsync(string apiKeyPlainText, CancellationToken cancellationToken = default)
    {
        var client = CreateAuthenticatedClient(apiKeyPlainText);
        using var response = await client.GetAsync("v1beta/models", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    protected override async Task<string> SendRawAsync(string system, string userMessage, string apiKey, CancellationToken cancellationToken)
    {
        var client = CreateAuthenticatedClient(apiKey);

        var request = new GeminiRequest
        {
            SystemInstruction = new GeminiContent { Parts = [new GeminiPart { Text = system }] },
            Contents = [new GeminiContent { Role = "user", Parts = [new GeminiPart { Text = userMessage }] }],
            GenerationConfig = new GeminiGenerationConfig { MaxOutputTokens = _options.MaxOutputTokens }
        };

        var url = $"v1beta/models/{_options.Model}:generateContent";
        using var httpResponse = await client.PostAsJsonAsync(url, request, JsonOptions, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            string? errorMessage = null;
            try
            {
                var errorBody = await httpResponse.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);
                errorMessage = errorBody?.Error?.Message;
            }
            catch (JsonException)
            {
                // Une panne en amont (proxy, passerelle) peut renvoyer du texte/HTML au lieu de JSON.
            }

            var message = $"Erreur API Gemini ({httpResponse.StatusCode}) : {errorMessage ?? "inconnue"}";
            if (IsTransientStatus(httpResponse.StatusCode))
            {
                throw new AiProviderTransientException(message);
            }
            throw new InvalidOperationException(message);
        }

        var body = await httpResponse.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Réponse vide de l'API Gemini.");

        var text = body.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrEmpty(text))
        {
            var finishReason = body.Candidates?.FirstOrDefault()?.FinishReason;
            throw new InvalidOperationException($"Aucun contenu texte dans la réponse Gemini (finishReason: {finishReason ?? "inconnu"}).");
        }

        return text;
    }

    private HttpClient CreateAuthenticatedClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient(nameof(GeminiAiProvider));
        client.BaseAddress = new Uri(_options.BaseUrl);
        client.DefaultRequestHeaders.Remove("x-goog-api-key");
        // Passée en en-tête plutôt qu'en paramètre de requête pour éviter qu'elle ne se retrouve
        // dans des logs d'accès (proxy, App Service) qui journalisent l'URL complète.
        client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
        return client;
    }
}

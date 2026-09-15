using System.Text.Json.Serialization;

namespace PfeCopilot.Infrastructure.Ai;

internal class AnthropicRequest
{
    [JsonPropertyName("model")] public required string Model { get; set; }
    [JsonPropertyName("max_tokens")] public required int MaxTokens { get; set; }
    [JsonPropertyName("system")] public string? System { get; set; }
    [JsonPropertyName("messages")] public required List<AnthropicMessage> Messages { get; set; }
}

internal class AnthropicMessage
{
    [JsonPropertyName("role")] public required string Role { get; set; }
    [JsonPropertyName("content")] public required string Content { get; set; }
}

internal class AnthropicResponse
{
    [JsonPropertyName("content")] public List<AnthropicContentBlock> Content { get; set; } = [];
    [JsonPropertyName("error")] public AnthropicError? Error { get; set; }
}

internal class AnthropicContentBlock
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("text")] public string? Text { get; set; }
}

internal class AnthropicError
{
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}

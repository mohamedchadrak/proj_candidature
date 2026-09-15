using System.Text.Json.Serialization;

namespace PfeCopilot.Infrastructure.Ai;

internal class GeminiRequest
{
    [JsonPropertyName("system_instruction")] public GeminiContent? SystemInstruction { get; set; }
    [JsonPropertyName("contents")] public required List<GeminiContent> Contents { get; set; }
    [JsonPropertyName("generationConfig")] public GeminiGenerationConfig? GenerationConfig { get; set; }
}

internal class GeminiContent
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("parts")] public required List<GeminiPart> Parts { get; set; }
}

internal class GeminiPart
{
    [JsonPropertyName("text")] public required string Text { get; set; }
}

internal class GeminiGenerationConfig
{
    [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
}

internal class GeminiResponse
{
    [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; }
    [JsonPropertyName("error")] public GeminiError? Error { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")] public GeminiContent? Content { get; set; }
    [JsonPropertyName("finishReason")] public string? FinishReason { get; set; }
}

internal class GeminiError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}

namespace PfeCopilot.Infrastructure.Ai;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
    public string ApiVersion { get; set; } = "2023-06-01";
    public string Model { get; set; } = "claude-sonnet-5";
    public int MaxTokens { get; set; } = 4096;
}

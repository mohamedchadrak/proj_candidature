namespace PfeCopilot.Infrastructure.Ai;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
    /// <summary>Alias "-latest" pour ne pas figer une version précise ; ajustez selon les modèles disponibles sur votre compte.</summary>
    public string Model { get; set; } = "gemini-flash-latest";
    public int MaxOutputTokens { get; set; } = 8192;
}

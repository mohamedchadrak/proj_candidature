namespace PfeCopilot.Application.Ai.Models;

/// <summary>Exigences extraites d'une offre d'emploi par l'IA, utilisées pour le matching et le ciblage ATS.</summary>
public class JobRequirements
{
    public string JobTitle { get; set; } = string.Empty;
    public string SeniorityLevel { get; set; } = string.Empty;
    public List<string> HardSkills { get; set; } = [];
    public List<string> SoftSkills { get; set; } = [];
    /// <summary>Mots-clés exacts à retrouver dans le CV pour maximiser le score ATS.</summary>
    public List<string> AtsKeywords { get; set; } = [];
    public string CompanyContext { get; set; } = string.Empty;
}

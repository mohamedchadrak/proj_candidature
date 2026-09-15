namespace PfeCopilot.Application.Ai.Models;

/// <summary>
/// Résultat de l'extraction fidèle d'un CV existant (PDF) en faits atomiques, proposé à
/// l'utilisateur pour relecture/correction avant d'être enregistré comme CvFact. L'IA ne doit
/// que retranscrire ce qui est écrit dans le CV, jamais compléter ou déduire des informations.
/// </summary>
public class ExtractedCvContent
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    public List<ExtractedCvFact> Facts { get; set; } = [];
}

public class ExtractedCvFact
{
    /// <summary>Nom d'une valeur de PfeCopilot.Domain.Enums.CvFactType (ex. "Experience", "Formation").</summary>
    public required string Type { get; set; }
    public required string Title { get; set; }
    public string? Organization { get; set; }
    public string? Description { get; set; }
    /// <summary>Format "yyyy-MM" si disponible dans le CV, sinon null.</summary>
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string Tags { get; set; } = string.Empty;
}

namespace PfeCopilot.Domain.Enums;

public enum CvFactType
{
    Experience,
    Formation,
    Projet,
    CompetenceTechnique,
    CompetenceTransversale,
    Langue,
    CentreInteret
}

public enum AiProviderType
{
    Claude,
    Gemini,
    OpenAi
}

public enum JobOfferSource
{
    FranceTravail,
    Adzuna,
    Jooble,
    Manuel
}

public enum JobApplicationStatus
{
    Brouillon,
    APostuler,
    Postule,
    Relance,
    Entretien,
    Reponse
}

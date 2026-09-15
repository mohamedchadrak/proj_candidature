namespace PfeCopilot.Infrastructure.JobBoards;

/// <summary>
/// Identifiants de l'application France Travail (Pôle emploi) "Offres d'emploi v2" — à créer
/// gratuitement sur https://francetravail.io. Configuration plateforme (pas par utilisateur) :
/// ce connecteur interroge une API publique d'offres, il n'engage pas de coût par appel.
/// </summary>
public class FranceTravailOptions
{
    public const string SectionName = "FranceTravail";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = "https://entreprise.francetravail.fr/connexion/oauth2/access_token?realm=%2Fpartenaire";
    public string ApiBaseUrl { get; set; } = "https://api.francetravail.io/partenaire/offresdemploi/v2/";
    public string Scope { get; set; } = "api_offresdemploiv2 o2dsoffre";
}

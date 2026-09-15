namespace PfeCopilot.Application.Common;

/// <summary>
/// Identité de l'utilisateur courant utilisée par les filtres globaux multi-tenant EF Core.
/// En contexte web, résolue depuis l'utilisateur HTTP authentifié. En contexte worker
/// (traitement système sans utilisateur "courant"), <see cref="UserId"/> est null : le
/// worker doit alors interroger explicitement avec <c>IgnoreQueryFilters()</c> et filtrer
/// lui-même par UserId offre par offre.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
}

using PfeCopilot.Application.Common;

namespace PfeCopilot.Infrastructure.Common;

/// <summary>
/// Implémentation neutre de <see cref="ICurrentUserService"/> : UserId toujours null, donc le
/// Global Query Filter d'ApplicationDbContext ne filtre rien. Utilisée par le worker de veille
/// (qui n'a pas d'utilisateur HTTP courant et filtre lui-même explicitement par UserId) ainsi
/// qu'au design-time pour générer les migrations EF Core.
/// </summary>
public class NullCurrentUserService : ICurrentUserService
{
    public string? UserId => null;
}

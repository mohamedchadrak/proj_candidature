using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Domain.Entities;

/// <summary>
/// Clé API d'un fournisseur IA appartenant à un utilisateur. La valeur en clair
/// n'est jamais persistée : <see cref="CipherText"/> est produit par
/// PfeCopilot.Application.Security.IApiKeyProtector avant d'atteindre la base.
/// </summary>
public class ApiKeyConfig : IUserOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserId { get; set; }

    public required AiProviderType Provider { get; set; }
    public required string CipherText { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAtUtc { get; set; }
}

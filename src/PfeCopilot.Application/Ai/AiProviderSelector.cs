using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;

namespace PfeCopilot.Application.Ai;

/// <summary>
/// Choisit quel <see cref="IAiProvider"/> utiliser pour un utilisateur donné : priorité à son
/// fournisseur préféré s'il a une clé active pour celui-ci, sinon le premier fournisseur pour
/// lequel il a configuré une clé active (permet de gérer plusieurs fournisseurs sans forcer
/// l'utilisateur à reconfigurer sa préférence à chaque ajout de clé).
/// </summary>
public static class AiProviderSelector
{
    public static (IAiProvider Provider, ApiKeyConfig ActiveKey) SelectActiveProvider(
        IReadOnlyCollection<IAiProvider> availableProviders,
        IReadOnlyCollection<ApiKeyConfig> activeKeys,
        AiProviderType preferredProvider)
    {
        var keysByProvider = activeKeys
            .Where(k => k.IsActive)
            .OrderByDescending(k => k.CreatedAtUtc)
            .GroupBy(k => k.Provider)
            .ToDictionary(g => g.Key, g => g.First());

        if (keysByProvider.Count == 0)
        {
            throw new InvalidOperationException("Aucune clé API active — ajoutez-en une dans vos paramètres.");
        }

        var orderedCandidates = keysByProvider.ContainsKey(preferredProvider)
            ? new[] { preferredProvider }.Concat(keysByProvider.Keys.Where(p => p != preferredProvider))
            : keysByProvider.Keys;

        foreach (var providerType in orderedCandidates)
        {
            var provider = availableProviders.FirstOrDefault(p => p.ProviderType == providerType);
            if (provider is not null)
            {
                return (provider, keysByProvider[providerType]);
            }
        }

        throw new InvalidOperationException(
            $"Aucun fournisseur IA disponible ne correspond à vos clés actives ({string.Join(", ", keysByProvider.Keys)}).");
    }
}

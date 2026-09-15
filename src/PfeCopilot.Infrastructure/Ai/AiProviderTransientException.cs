namespace PfeCopilot.Infrastructure.Ai;

/// <summary>
/// Erreur temporaire du fournisseur IA (surcharge, rate limit, indisponibilité momentanée) —
/// à retenter automatiquement avec un délai croissant, contrairement à une clé invalide ou une
/// requête malformée qui ne se résoudront jamais en réessayant.
/// </summary>
public class AiProviderTransientException(string message) : Exception(message);

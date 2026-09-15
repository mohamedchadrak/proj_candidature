using Microsoft.AspNetCore.DataProtection;
using PfeCopilot.Application.Security;

namespace PfeCopilot.Infrastructure.Security;

/// <summary>
/// Chiffre les clés API avant stockage en base avec ASP.NET Core Data Protection.
/// La clé de protection elle-même est persistée hors base (disque local en dev,
/// Azure Blob Storage protégé par Key Vault en prod) — voir DependencyInjection.cs.
/// </summary>
public class ApiKeyProtector : IApiKeyProtector
{
    private const string Purpose = "PfeCopilot.ApiKeys.v1";
    private readonly IDataProtector _protector;

    public ApiKeyProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plainTextApiKey) => _protector.Protect(plainTextApiKey);

    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}

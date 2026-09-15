namespace PfeCopilot.Application.Security;

/// <summary>Chiffrement/déchiffrement des clés API stockées en base (ASP.NET Core Data Protection).</summary>
public interface IApiKeyProtector
{
    string Protect(string plainTextApiKey);
    string Unprotect(string cipherText);
}

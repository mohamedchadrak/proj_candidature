using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PfeCopilot.Application.Common;

namespace PfeCopilot.Infrastructure.Common;

/// <summary>
/// Résout l'utilisateur courant pour l'application Blazor Server. Utilise
/// AuthenticationStateProvider plutôt que IHttpContextAccessor : en rendu interactif
/// (circuits SignalR), le HttpContext de la requête initiale n'est plus disponible,
/// alors que l'état d'authentification du circuit, lui, l'est.
/// </summary>
public class BlazorCurrentUserService(AuthenticationStateProvider authenticationStateProvider) : ICurrentUserService
{
    public string? UserId
    {
        get
        {
            var authState = authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult();
            return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}

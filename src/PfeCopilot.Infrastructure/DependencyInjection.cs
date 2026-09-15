using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PfeCopilot.Application.Ai;
using PfeCopilot.Application.Applications;
using PfeCopilot.Application.Common;
using PfeCopilot.Application.CvImport;
using PfeCopilot.Application.JobBoards;
using PfeCopilot.Application.Latex;
using PfeCopilot.Application.Security;
using PfeCopilot.Infrastructure.Ai;
using PfeCopilot.Infrastructure.CvImport;
using PfeCopilot.Infrastructure.Identity;
using PfeCopilot.Infrastructure.JobBoards;
using PfeCopilot.Infrastructure.Latex;
using PfeCopilot.Infrastructure.Security;

namespace PfeCopilot.Infrastructure;

/// <summary>
/// Enregistre tous les services métier (DB, IA, connecteurs offres, LaTeX, sécurité).
/// L'enregistrement de <see cref="ICurrentUserService"/> reste à la charge de chaque hôte
/// (Web : utilisateur du circuit Blazor ; Worker : PfeCopilot.Infrastructure.Common.NullCurrentUserService)
/// car sa résolution diffère fondamentalement entre un contexte web et un contexte batch.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPfeCopilotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<FranceTravailOptions>(configuration.GetSection(FranceTravailOptions.SectionName));
        services.Configure<AdzunaOptions>(configuration.GetSection(AdzunaOptions.SectionName));
        services.Configure<JoobleOptions>(configuration.GetSection(JoobleOptions.SectionName));

        services.AddHttpClient();

        services.AddScoped<IApiKeyProtector, ApiKeyProtector>();
        services.AddScoped<IAiProvider, ClaudeAiProvider>();
        services.AddScoped<IAiProvider, GeminiAiProvider>();
        services.AddScoped<IJobBoardConnector, FranceTravailConnector>();
        services.AddScoped<IJobBoardConnector, AdzunaConnector>();
        services.AddScoped<IJobBoardConnector, JoobleConnector>();
        services.AddScoped<IOfferTextExtractor, OfferTextExtractor>();
        services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
        services.AddSingleton<ILatexTemplateRenderer, LatexTemplateRenderer>();
        services.AddScoped<ILatexPdfCompiler, LatexPdfCompiler>();

        services.AddScoped<GenerateApplicationHandler>();
        services.AddScoped<ImportCvFromPdfHandler>();

        return services;
    }
}

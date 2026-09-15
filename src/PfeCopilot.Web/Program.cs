using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PfeCopilot.Application.Common;
using PfeCopilot.Web.Components;
using PfeCopilot.Web.Components.Account;
using PfeCopilot.Infrastructure;
using PfeCopilot.Infrastructure.Common;
using PfeCopilot.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Tolérance accrue aux coupures de circuit : l'App Service tourne sur un plan gratuit (F1) qui
// n'a pas d'"Always On" et peut se mettre en veille après inactivité — le réveil (cold start) et
// les ralentissements CPU occasionnels du plan partagé prennent plus de temps que les délais par
// défaut de SignalR/Blazor Server, ce qui déclenche des déconnexions ("Rejoin failed") évitables.
builder.Services.Configure<CircuitOptions>(options =>
{
    options.DisconnectedCircuitMaxRetained = 50;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    options.JSInteropDefaultCallTimeout = TimeSpan.FromSeconds(60);
});
builder.Services.Configure<HubOptions>(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(20);
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<ICurrentUserService, BlazorCurrentUserService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Enregistre ApplicationDbContext (SQL Server), l'IA, les connecteurs offres, le LaTeX, etc.
builder.Services.AddPfeCopilotInfrastructure(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Data Protection protège les clés API stockées en base (ApiKeyProtector). L'App Service peut
// tourner sur plusieurs instances : le trousseau de clés doit donc être persisté hors de
// l'instance locale pour que toutes les instances (et PfeCopilot.Worker) déchiffrent les mêmes
// clés — voir deploy/azure/main.bicep pour le compte de stockage et l'identité managée associée.
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("PfeCopilot");
var blobStorageAccountUri = builder.Configuration["DataProtection:BlobStorageAccountUri"];
if (!string.IsNullOrWhiteSpace(blobStorageAccountUri))
{
    var keysBlobUri = new Uri($"{blobStorageAccountUri.TrimEnd('/')}/dataprotection-keys/keys.xml");
    dataProtectionBuilder.PersistKeysToAzureBlobStorage(keysBlobUri, new Azure.Identity.DefaultAzureCredential());
}
else if (builder.Environment.IsDevelopment())
{
    // Dossier partagé avec PfeCopilot.Worker (à la racine du dépôt) : les deux hôtes doivent
    // utiliser le même trousseau de clés pour pouvoir déchiffrer les mêmes clés API en base.
    var sharedKeysPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "App_Data", "keys"));
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(sharedKeysPath));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Ces endpoints utilisent directement HttpContext.User (et non ICurrentUserService/BlazorCurrentUserService,
// qui dépend d'un circuit Blazor via AuthenticationStateProvider et n'a pas de sens pour une requête HTTP nue).
static string? GetUserId(HttpContext httpContext) =>
    httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

app.MapGet("/api/applications/{id:guid}/tex", async (Guid id, HttpContext httpContext, IAppDbContext db) =>
{
    var userId = GetUserId(httpContext);
    var application = await db.JobApplications.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    if (application is null)
    {
        return Results.NotFound();
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(application.LatexSource);
    return Results.File(bytes, "application/x-tex", $"candidature-{id}.tex");
}).RequireAuthorization();

app.MapGet("/api/applications/{id:guid}/pdf", async (Guid id, HttpContext httpContext, IAppDbContext db, PfeCopilot.Application.Latex.ILatexPdfCompiler compiler) =>
{
    var userId = GetUserId(httpContext);
    var application = await db.JobApplications.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    if (application is null)
    {
        return Results.NotFound();
    }

    var result = await compiler.CompileAsync(application.LatexSource);
    if (!result.Success || result.PdfBytes is null)
    {
        return Results.Problem(
            title: "Échec de la compilation LaTeX",
            detail: result.Log,
            statusCode: StatusCodes.Status500InternalServerError);
    }

    return Results.File(result.PdfBytes, "application/pdf", $"candidature-{id}.pdf");
}).RequireAuthorization();

app.Run();

using Microsoft.AspNetCore.DataProtection;
using PfeCopilot.Application.Common;
using PfeCopilot.Infrastructure;
using PfeCopilot.Infrastructure.Common;
using PfeCopilot.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Traitement système sans utilisateur HTTP courant : le filtre global multi-tenant d'ApplicationDbContext
// est donc neutralisé, et JobWatchBackgroundService filtre lui-même explicitement par UserId.
builder.Services.AddSingleton<ICurrentUserService, NullCurrentUserService>();
builder.Services.AddPfeCopilotInfrastructure(builder.Configuration);

// Le worker n'appelle jamais IApiKeyProtector lui-même, mais GenerateApplicationHandler (enregistré
// par AddPfeCopilotInfrastructure) en dépend transitivement : Data Protection doit donc être
// configuré ici aussi, avec le même trousseau de clés que PfeCopilot.Web pour rester compatible.
var dataProtectionBuilder = builder.Services.AddDataProtection().SetApplicationName("PfeCopilot");
var blobStorageAccountUri = builder.Configuration["DataProtection:BlobStorageAccountUri"];
if (!string.IsNullOrWhiteSpace(blobStorageAccountUri))
{
    var keysBlobUri = new Uri($"{blobStorageAccountUri.TrimEnd('/')}/dataprotection-keys/keys.xml");
    dataProtectionBuilder.PersistKeysToAzureBlobStorage(keysBlobUri, new Azure.Identity.DefaultAzureCredential());
}
else if (builder.Environment.IsDevelopment())
{
    var sharedKeysPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "App_Data", "keys"));
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(sharedKeysPath));
}

builder.Services.AddHostedService<JobWatchBackgroundService>();

var host = builder.Build();
host.Run();

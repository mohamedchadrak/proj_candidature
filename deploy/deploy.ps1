#Requires -Version 7.0
<#
.SYNOPSIS
    Déploie PFE Copilot sur Azure : provisionne l'infrastructure (Bicep) puis publie le code
    de PfeCopilot.Web et applique les migrations EF Core sur la base Azure SQL.

.DESCRIPTION
    Prérequis :
      - Azure CLI installé et connecté (az login)
      - dotnet-ef installé (dotnet tool install --global dotnet-ef)
      - Un mot de passe administrateur SQL fort prêt à être fourni

    Ce script est idempotent : il peut être relancé pour mettre à jour une installation existante
    (le Bicep utilise "az deployment group create" qui applique uniquement les changements).

.PARAMETER ResourceGroupName
    Nom du groupe de ressources Azure (créé s'il n'existe pas).

.PARAMETER Location
    Région Azure (ex. "westeurope", "francecentral").

.PARAMETER NamePrefix
    Préfixe court pour nommer les ressources (ex. "pfecopilot").

.PARAMETER SqlAdminLogin
    Login administrateur du serveur Azure SQL.

.EXAMPLE
    ./deploy.ps1 -ResourceGroupName rg-pfecopilot -Location francecentral -NamePrefix pfecopilot -SqlAdminLogin sqladmin
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $false)]
    [string]$NamePrefix = "pfecopilot",

    [Parameter(Mandatory = $true)]
    [string]$SqlAdminLogin,

    [Parameter(Mandatory = $false)]
    [switch]$SkipInfrastructure,

    [Parameter(Mandatory = $false)]
    [switch]$SkipMigrations
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

Write-Host "== PFE Copilot — Déploiement Azure ==" -ForegroundColor Cyan

# ---------- 1. Vérification des prérequis ----------
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) introuvable. Installez-le : https://aka.ms/installazurecliwindows"
}

$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    throw "Vous n'êtes pas connecté à Azure CLI. Lancez d'abord : az login"
}
Write-Host "Abonnement Azure actif : $($account.name) ($($account.id))" -ForegroundColor Green

# ---------- 2. Provisionnement de l'infrastructure (Bicep) ----------
if (-not $SkipInfrastructure) {
    Write-Host "`n-- Création/mise à jour du groupe de ressources '$ResourceGroupName' --" -ForegroundColor Cyan
    az group create --name $ResourceGroupName --location $Location --output none

    $sqlAdminPassword = Read-Host -Prompt "Mot de passe administrateur SQL (min. 12 caractères, majuscule+minuscule+chiffre+symbole)" -AsSecureString
    $sqlAdminPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlAdminPassword))

    Write-Host "`n-- Déploiement du template Bicep (deploy/azure/main.bicep) --" -ForegroundColor Cyan
    $deployment = az deployment group create `
        --resource-group $ResourceGroupName `
        --template-file (Join-Path $scriptRoot "azure/main.bicep") `
        --parameters namePrefix=$NamePrefix location=$Location sqlAdminLogin=$SqlAdminLogin sqlAdminPassword=$sqlAdminPasswordPlain `
        --output json | ConvertFrom-Json

    $webAppName = $deployment.properties.outputs.webAppName.value
    $sqlServerFqdn = $deployment.properties.outputs.sqlServerFqdn.value
    Write-Host "App Service créé : $webAppName" -ForegroundColor Green
    Write-Host "Serveur SQL : $sqlServerFqdn" -ForegroundColor Green
}
else {
    Write-Host "`n-- Infrastructure ignorée (-SkipInfrastructure) : récupération de l'App Service existant --" -ForegroundColor Yellow
    $webAppName = az webapp list --resource-group $ResourceGroupName --query "[?starts_with(name, '$NamePrefix-web')].name | [0]" -o tsv
    if (-not $webAppName) {
        throw "Aucun App Service '$NamePrefix-web*' trouvé dans '$ResourceGroupName'. Retirez -SkipInfrastructure pour le créer."
    }
}

# ---------- 3. Publication du code de PfeCopilot.Web ----------
Write-Host "`n-- Publication de PfeCopilot.Web --" -ForegroundColor Cyan
$publishDir = Join-Path $repoRoot "deploy/.publish/web"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish (Join-Path $repoRoot "src/PfeCopilot.Web/PfeCopilot.Web.csproj") `
    -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Échec de 'dotnet publish' pour PfeCopilot.Web." }

$zipPath = Join-Path $repoRoot "deploy/.publish/web.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "-- Déploiement du zip sur l'App Service '$webAppName' --" -ForegroundColor Cyan
az webapp deploy --resource-group $ResourceGroupName --name $webAppName --src-path $zipPath --type zip --output none
Write-Host "PfeCopilot.Web déployé." -ForegroundColor Green

# ---------- 4. Migrations EF Core sur Azure SQL ----------
if (-not $SkipMigrations) {
    Write-Host "`n-- Application des migrations EF Core sur Azure SQL --" -ForegroundColor Cyan
    Write-Host "Cette étape se connecte directement à la base : assurez-vous que votre IP est autorisée" -ForegroundColor Yellow
    Write-Host "sur le pare-feu du serveur SQL (az sql server firewall-rule create ...)." -ForegroundColor Yellow

    $connectionString = az webapp config appsettings list --resource-group $ResourceGroupName --name $webAppName `
        --query "[?name=='ConnectionStrings__DefaultConnection'].value | [0]" -o tsv

    if (-not $connectionString -or $connectionString -like "*Microsoft.KeyVault*") {
        Write-Host "Impossible de récupérer une chaîne de connexion exploitable en clair (référence Key Vault)." -ForegroundColor Yellow
        Write-Host "Fournissez-la manuellement :" -ForegroundColor Yellow
        $connectionString = Read-Host -Prompt "Chaîne de connexion Azure SQL complète"
    }

    dotnet ef database update `
        --project (Join-Path $repoRoot "src/PfeCopilot.Infrastructure/PfeCopilot.Infrastructure.csproj") `
        --startup-project (Join-Path $repoRoot "src/PfeCopilot.Web/PfeCopilot.Web.csproj") `
        --context PfeCopilot.Infrastructure.Identity.ApplicationDbContext `
        --connection $connectionString
    if ($LASTEXITCODE -ne 0) { throw "Échec de l'application des migrations EF Core." }

    Write-Host "Migrations appliquées." -ForegroundColor Green
}

Write-Host "`n== Déploiement terminé ==" -ForegroundColor Cyan
Write-Host "URL : https://$webAppName.azurewebsites.net" -ForegroundColor Green
Write-Host "`nÀ faire manuellement si ce n'est pas déjà fait :" -ForegroundColor Yellow
Write-Host "  - Configurer les clés FranceTravail/Adzuna dans les paramètres de l'App Service (ou Key Vault)."
Write-Host "  - Déployer PfeCopilot.Worker (2e App Service Linux 'Always On', ou Azure Container Apps / WebJob)."
Write-Host "  - Configurer un vrai IEmailSender (SMTP/SendGrid) pour remplacer IdentityNoOpEmailSender en production."

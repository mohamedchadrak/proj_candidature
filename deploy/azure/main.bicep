// Infrastructure Azure pour PFE Copilot : App Service (Web + Worker), Azure SQL, Key Vault,
// Storage (trousseau Data Protection partagé entre les deux App Services), Application Insights.
targetScope = 'resourceGroup'

@description('Préfixe court utilisé pour nommer toutes les ressources (ex. "pfecopilot").')
param namePrefix string = 'pfecopilot'

@description('Région Azure de déploiement.')
param location string = resourceGroup().location

@description('Login administrateur du serveur Azure SQL.')
param sqlAdminLogin string

@secure()
@description('Mot de passe administrateur du serveur Azure SQL.')
param sqlAdminPassword string

@description('SKU du plan App Service (B1 = économique, suffisant pour un MVP).')
param appServicePlanSku string = 'B1'

var sqlServerName = '${namePrefix}-sql-${uniqueString(resourceGroup().id)}'
var sqlDatabaseName = 'PfeCopilot'
var storageAccountName = toLower('${namePrefix}st${uniqueString(resourceGroup().id)}')
var keyVaultName = '${namePrefix}-kv-${uniqueString(resourceGroup().id)}'
var appServicePlanName = '${namePrefix}-plan'
var webAppName = '${namePrefix}-web-${uniqueString(resourceGroup().id)}'
var appInsightsName = '${namePrefix}-ai'
var logAnalyticsName = '${namePrefix}-logs'

// ---------- Base de données ----------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'GP_S_Gen5_1' // Serverless General Purpose — coût minimal pour un MVP à faible trafic.
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
  }
}

resource sqlFirewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ---------- Stockage (trousseau Data Protection partagé) ----------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource keysContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'dataprotection-keys'
  properties: {
    publicAccess: 'None'
  }
}

// ---------- Key Vault (secrets : connection string, clé de chiffrement Data Protection) ----------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
  }
}

// ---------- Observabilité ----------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ---------- App Service (héberge PfeCopilot.Web ; le Worker peut être déployé en 2e Web App "Always On"
// exécutant le même binaire en mode "WEBSITES_CONTAINER_START_TIME_LIMIT" étendu, ou en Azure Container App —
// laissé volontairement simple ici avec un plan Linux mutualisé, voir deploy.ps1 pour le déploiement du code) ----------
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      appSettings: [
        { name: 'ConnectionStrings__DefaultConnection', value: '@Microsoft.KeyVault(SecretUri=${sqlConnectionStringSecret.properties.secretUri})' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'DataProtection__BlobStorageAccountUri', value: storageAccount.properties.primaryEndpoints.blob }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      ]
    }
  }
}

// Autorise l'identité managée de l'App Service à lire les secrets Key Vault (rôle "Key Vault Secrets User").
resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Autorise l'identité managée à lire/écrire le conteneur de trousseau Data Protection (rôle "Storage Blob Data Contributor").
resource storageBlobContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, webApp.id, 'StorageBlobDataContributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output webAppName string = webApp.name
output webAppDefaultHostName string = webApp.properties.defaultHostName
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output keyVaultName string = keyVault.name
output storageAccountName string = storageAccount.name

targetScope = 'subscription'

@description('Azure region for all resources.')
param location string = 'chilecentral'

@description('Region for Log Analytics and Microsoft Sentinel.')
param monitoringLocation string = 'brazilsouth'

@description('Resource group created by this subscription deployment.')
param resourceGroupName string = 'rg-bancoseguro-s5-dev'

@description('Set true only after private connectivity and administration have been validated.')
param restrictPublicNetworkAccess bool = false

@description('Required Microsoft Entra object ID for the Azure SQL administrator.')
@minLength(36)
@maxLength(36)
param sqlEntraAdminObjectId string

@description('Required display name of the Microsoft Entra administrator for Azure SQL.')
@minLength(1)
param sqlEntraAdminLogin string

var suffix = uniqueString(subscription().id, resourceGroupName)
var appName = 'app-bancoseguro-dev-${suffix}'
var keyVaultName = 'kv-bs-dev-${suffix}'
var sqlServerName = 'sql-bancoseguro-dev-${suffix}'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module network 'modules/network.bicep' = {
  name: 'network'
  scope: resourceGroup
  params: {
    location: location
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    location: location
    workspaceLocation: monitoringLocation
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  scope: resourceGroup
  params: {
    location: location
    keyVaultName: keyVaultName
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    restrictPublicNetworkAccess: restrictPublicNetworkAccess
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: resourceGroup
  params: {
    location: location
    sqlServerName: sqlServerName
    databaseName: 'sqldb-bancoseguro'
    entraAdminObjectId: sqlEntraAdminObjectId
    entraAdminLogin: sqlEntraAdminLogin
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    restrictPublicNetworkAccess: restrictPublicNetworkAccess
  }
}

module appService 'modules/appservice.bicep' = {
  name: 'appService'
  scope: resourceGroup
  params: {
    location: location
    appName: appName
    integrationSubnetId: network.outputs.appServiceIntegrationSubnetId
    keyVaultName: keyVaultName
    keyVaultUri: keyVault.outputs.vaultUri
    sqlServerFqdn: sql.outputs.serverFqdn
    databaseName: sql.outputs.databaseName
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
  }
}

module privateEndpoints 'modules/private-endpoints.bicep' = {
  name: 'privateEndpoints'
  scope: resourceGroup
  params: {
    location: location
    vnetId: network.outputs.vnetId
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    keyVaultId: keyVault.outputs.vaultId
    sqlServerId: sql.outputs.serverId
  }
}

module sentinel 'modules/sentinel.bicep' = {
  name: 'sentinel'
  scope: resourceGroup
  params: {
    workspaceName: monitoring.outputs.workspaceName
  }
}

output resourceGroupName string = resourceGroup.name
output appServiceName string = appService.outputs.appName
output appServiceUrl string = appService.outputs.appUrl
output keyVaultName string = keyVault.outputs.vaultName
output sqlServerName string = sql.outputs.serverName
output sqlDatabaseName string = sql.outputs.databaseName
output logAnalyticsWorkspaceName string = monitoring.outputs.workspaceName

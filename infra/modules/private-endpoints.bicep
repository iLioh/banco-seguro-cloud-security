param location string
param vnetId string
param privateEndpointSubnetId string
param keyVaultId string
param sqlServerId string
param internalApiId string

resource keyVaultPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
}

resource sqlPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink${environment().suffixes.sqlServerHostname}'
  location: 'global'
}

resource internalApiPrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.azurewebsites.net'
  location: 'global'
}

resource internalApiDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: internalApiPrivateDnsZone
  name: 'link-bancoseguro-vnet'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

resource keyVaultDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: keyVaultPrivateDnsZone
  name: 'link-bancoseguro-vnet'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

resource sqlDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: sqlPrivateDnsZone
  name: 'link-bancoseguro-vnet'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pep-bancoseguro-keyvault'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'keyvault-connection'
        properties: {
          privateLinkServiceId: keyVaultId
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource keyVaultDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: keyVaultPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'keyvault-zone'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZone.id
        }
      }
    ]
  }
}

resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pep-bancoseguro-sql'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'sql-connection'
        properties: {
          privateLinkServiceId: sqlServerId
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

resource sqlDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: sqlPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql-zone'
        properties: {
          privateDnsZoneId: sqlPrivateDnsZone.id
        }
      }
    ]
  }
}

resource internalApiPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pep-bancoseguro-internal-api'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'internal-api-connection'
        properties: {
          privateLinkServiceId: internalApiId
          groupIds: [
            'sites'
          ]
        }
      }
    ]
  }
}

resource internalApiDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: internalApiPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'internal-api-zone'
        properties: {
          privateDnsZoneId: internalApiPrivateDnsZone.id
        }
      }
    ]
  }
}

output keyVaultPrivateEndpointId string = keyVaultPrivateEndpoint.id
output sqlPrivateEndpointId string = sqlPrivateEndpoint.id
output internalApiPrivateEndpointId string = internalApiPrivateEndpoint.id

param workspaceName string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

resource sentinelOnboarding 'Microsoft.SecurityInsights/onboardingStates@2025-09-01' = {
  name: 'default'
  scope: workspace
  properties: {
    customerManagedKey: false
  }
}

output onboardingStateId string = sentinelOnboarding.id

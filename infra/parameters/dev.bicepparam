// =====================================================
// Parameters — ambiente "dev" (default para o evento)
// =====================================================
// Usar:
//   az deployment group create \
//     --resource-group fifa2026-rg \
//     --template-file main.bicep \
//     --parameters parameters/dev.bicepparam
// =====================================================
using '../main.bicep'

param namingPrefix = 'fifa2026'
param location = 'eastus2'
param appServicePlanSku = 'B1'
param sqlDatabaseSku = 'Basic'
param sqlAdminLogin = 'fifa2026admin'
param sqlDatabaseName = 'FIFA2026Tickets'
param nodeVersion = '~18'

// Senhas via variáveis de ambiente — não commitar valores!
//   export SQL_ADMIN_PASSWORD='...'
//   export JWT_SECRET='...'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', 'TROQUE_AQUI_OU_VIA_ENV')
param jwtSecret = readEnvironmentVariable('JWT_SECRET', 'TROQUE_AQUI_OU_VIA_ENV')

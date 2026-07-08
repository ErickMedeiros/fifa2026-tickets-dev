// =====================================================
// Azure SQL Server + Database
// =====================================================
// Servidor lógico + 1 banco "FIFA2026Tickets".
//
// Firewall / rede (EPIC-004 Story 4.2 / ADE-009 Inv 4):
//   - A regra de firewall curinga "permitir todos os serviços Azure" (a faixa que abarcava
//     todo IP de todo tenant) foi REMOVIDA. Ela NÃO era "os serviços do lab" — era QUALQUER
//     recurso de QUALQUER tenant Azure (exposição inter-tenant). Sai por defense-in-depth,
//     independente da força da senha (e com MI-AAD nem há senha — Story 4.1).
//   - T1 (padrão, US$0): `publicNetworkAccess: 'Enabled'` SEM a regra wildcard. O acesso
//     passa a depender de IDENTIDADE (MI-AAD, Story 4.1) — "alcançável na rede, mas só
//     autentica quem tem token AAD válido para uma identidade com grant". Regras de
//     firewall ESTREITAS (se a conectividade da turma exigir) são provisionadas pelo
//     @devops em runtime — nunca o wildcard.
//   - T2 (showcase, ~US$7/mês): `enablePrivateEndpoint = true` → Private Endpoint +
//     `publicNetworkAccess: 'Disabled'`. Pré-provisionado pelo instrutor fora do relógio
//     da aula (ADE-009 Inv 3: VNet+CAE nascem primeiro — o CAE é imutável).
//
// ADE-009 Inv 2 / Story 4.1 (AC-4): Azure AD admin no servidor SQL.
//   Pré-requisito para autenticação Entra/Managed Identity (os contained users
//   `FROM EXTERNAL PROVIDER` da @data-engineer NÃO funcionam sem um AD admin no
//   servidor). Recurso OPCIONAL e CONDICIONAL (só materializa quando o objectId é
//   fornecido) — mantém compatível o deploy legado (main.bicep não passa os params).
//   O objectId/login vêm de parâmetro (nunca hardcoded — ADE-003 Inv 3); o valor real
//   é definido no provisionamento (@devops). O SQL auth (administratorLogin/Password)
//   PERMANECE (AC-5) — hardening aditivo, não Entra-only.
// =====================================================

@description('Nome do servidor SQL (sem .database.windows.net).')
param serverName string

@description('Nome do database.')
param databaseName string

@description('Região Azure.')
param location string

@description('Login admin (SQL auth).')
param adminLogin string

@description('Senha admin.')
@secure()
param adminPassword string

@description('SKU da database.')
param skuName string

// --- ADE-009 Inv 2 / Story 4.1 (AC-4) — Azure AD admin (opcional, condicional) ---
@description('Login/nome de exibição do Azure AD admin do SQL Server (UPN de usuário ou nome de grupo). Vazio = não cria o AD admin (deploy legado).')
param aadAdminLogin string = ''

@description('Object ID (sid) do principal Entra que será AD admin do SQL Server. Vazio = não cria o AD admin. Definido no provisionamento (@devops) — nunca hardcoded.')
param aadAdminObjectId string = ''

@description('Tenant ID do principal Entra do AD admin. Default = tenant da subscription do deploy (derivado, não hardcoded).')
param aadAdminTenantId string = subscription().tenantId

// --- EPIC-004 Story 4.2 (ADE-009 Inv 4) — trilha T2 (showcase): SQL Private Endpoint ---
@description('T2 (showcase): quando true, cria um Private Endpoint para o SQL e seta publicNetworkAccess=Disabled. Default false = T1 (US$0): publicNetworkAccess=Enabled SEM a regra de firewall curinga que abria o SQL a todo servico Azure.')
param enablePrivateEndpoint bool = false

@description('Resource ID da subnet (VNet do CAE VNet-integrado) onde o NIC do Private Endpoint do SQL será criado. Obrigatório quando enablePrivateEndpoint=true; provisionado pelo @devops (nunca hardcoded — ADE-003 Inv 3).')
param privateEndpointSubnetId string = ''

@description('(opcional) Resource ID da Private DNS Zone privatelink.database.windows.net a vincular ao Private Endpoint. Vazio = @devops cuida do DNS no provisionamento. Só usado quando enablePrivateEndpoint=true.')
param privateDnsZoneId string = ''

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    // ADE-009 Inv 4: T1 mantém Enabled (sem wildcard); T2 (Private Endpoint) fecha por
    // completo o acesso público.
    publicNetworkAccess: enablePrivateEndpoint ? 'Disabled' : 'Enabled'
    version: '12.0'
  }
}

// ADE-009 Inv 2 / Story 4.1 (AC-4) — Azure AD admin do servidor (pré-requisito da
// autenticação Entra/MI e dos contained users FROM EXTERNAL PROVIDER). Condicional:
// só materializa quando aadAdminObjectId é fornecido (deploy legado sem os params
// continua válido). name='ActiveDirectory' e administratorType='ActiveDirectory' são
// os valores fixos exigidos pela API (AC-16 — sem invenção).
resource sqlAadAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = if (!empty(aadAdminObjectId)) {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: aadAdminLogin
    sid: aadAdminObjectId
    tenantId: aadAdminTenantId
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: skuName
    tier: skuName == 'Basic' ? 'Basic' : 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: skuName == 'Basic' ? 2147483648 : 268435456000
    zoneRedundant: false
  }
}

// --- EPIC-004 Story 4.2 (ADE-009 Inv 4) — Private Endpoint (T2 showcase, condicional) ---
// Só materializa quando enablePrivateEndpoint=true. groupId 'sqlServer' é o valor fixo
// exigido pela API para Private Link de Microsoft.Sql/servers (AC-15 — sem invenção).
resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = if (enablePrivateEndpoint) {
  name: '${serverName}-pe'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${serverName}-plsc'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

// Vincula a Private DNS Zone (privatelink.database.windows.net) ao Private Endpoint, se
// fornecida. Condicional dentro da trilha T2 — sem zona, o @devops resolve o DNS à parte.
resource sqlPeDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = if (enablePrivateEndpoint && !empty(privateDnsZoneId)) {
  parent: sqlPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-database-windows-net'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output serverName string = sqlServer.name
output databaseName string = sqlDb.name
output privateEndpointId string = enablePrivateEndpoint ? sqlPrivateEndpoint.id : ''

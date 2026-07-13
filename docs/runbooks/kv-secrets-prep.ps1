<#
Prepara valores das secrets do Key Vault kv-dev-tk-cin-001 (Fase 1.4 do final-portal-guide.md).

Uso:
  pwsh ./docs/runbooks/kv-secrets-prep.ps1
  pwsh ./docs/runbooks/kv-secrets-prep.ps1 -RotateGatewaySecret

Saída: imprime na tela os valores prontos para colar no Portal (Objects → Secrets → +Generate/Import).
Depois de colar, LIMPE o buffer do terminal (Ctrl+L / Clear-Host) — senha fica visível.
#>

[CmdletBinding()]
param(
  [string]$SqlServerHost = 'fifa2026-sql-erick2026b.database.windows.net',
  [string]$Database = 'FIFA2026Tickets',
  [string]$SqlUser,
  [switch]$RotateGatewaySecret
)

if (-not $SqlUser) {
  $SqlUser = Read-Host 'SQL admin user (ex.: sqladmin)'
}

$sec = Read-Host 'Senha do SQL admin' -AsSecureString
$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
$sqlPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) | Out-Null

$sqlConn = "Server=tcp:$SqlServerHost,1433;Database=$Database;User Id=$SqlUser;Password=$sqlPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host ''
Write-Host '===== SECRETS DO KEY VAULT kv-dev-tk-cin-001 (Fase 1.4) =====' -ForegroundColor Cyan

Write-Host ''
Write-Host '[3] sql-connection-string' -ForegroundColor Yellow
Write-Host $sqlConn

Write-Host ''
Write-Host '[5] backend-sql-password' -ForegroundColor Yellow
Write-Host $sqlPassword

if ($RotateGatewaySecret) {
  $bytes = New-Object byte[] 24
  [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
  $newSecret = -join ($bytes | ForEach-Object { $_.ToString('x2') })

  Write-Host ''
  Write-Host '[1] gateway-admin-shared-secret (NOVO — precisa reescrever o GATEWAY_SHARED_SECRET no fork)' -ForegroundColor Yellow
  Write-Host $newSecret
  Write-Host ''
  Write-Host 'Comando para sincronizar o fork:' -ForegroundColor DarkGray
  Write-Host "gh secret set GATEWAY_SHARED_SECRET --repo ErickMedeiros/fifa2026-tickets-dev --body '$newSecret'" -ForegroundColor DarkGray
}

Write-Host ''
Write-Host '===== Pendentes (não gerados por este script) =====' -ForegroundColor Cyan
Write-Host '[1] gateway-admin-shared-secret  — valor atual do secret GATEWAY_SHARED_SECRET do fork (ou -RotateGatewaySecret p/ gerar novo).'
Write-Host '[2] gemini-api-key               — mesmo valor cadastrado no secret GEMINI_API_KEY do fork (chave da Fase 0).'
Write-Host '[4] servicebus-connection-string — Portal: Function App func-prod-tk-cin-001 -> Configuration -> ServiceBusConnection.'
Write-Host ''
Write-Host 'Depois de colar tudo no Portal, rode Clear-Host para apagar o buffer.' -ForegroundColor Red

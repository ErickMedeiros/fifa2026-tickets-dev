# Pacote pronto para os alunos — FIFA 2026 Tickets

Este guia descreve como **gerar um pacote compilado e pronto** (`fifa2026-api.zip` +
`fifa2026-web.zip`) para os alunos publicarem nas VMs **sem precisar compilar nada**, e
como os alunos configuram cada peça.

> **Por que isso funciona sem recompilar:**
> - O **backend** é JavaScript puro (Node + Express). Não há etapa de build — "pronto"
>   significa apenas o código-fonte + `node_modules` instalado. As dependências
>   (`bcryptjs`, `mssql`, …) são todas JS puro, sem módulos nativos, então o
>   `node_modules` é portável entre máquinas Windows.
> - O **frontend** é buildado **uma única vez** (Vite → `dist/`). O IP do backend
>   **não** fica embutido no JavaScript (o código usa a URL relativa `/api`); ele vive
>   apenas no `web.config`, no placeholder `__BACKEND_URL__`. Logo, o **mesmo** frontend
>   compilado serve para qualquer aluno — basta cada um editar **uma linha** do
>   `web.config`.

---

## PARTE A — Professor: gerar o pacote (repetível)

Pré-requisitos na máquina do professor: **Node.js 18+** e **npm**.
Rode no PowerShell, a partir da raiz do repositório.

```powershell
$base = "C:\Projetos-aios\TFTEC-Copa-Mundo-2026\FIFA2026-APP"
$fe   = Join-Path $base "Lovable\World Cup Tickets Hub"
$stg  = Join-Path $base "_pacote-alunos"

# 1. Build do frontend (gera dist/). O script crava localhost por padrão.
Set-Location $fe
npm ci          # só na 1ª vez ou quando package-lock mudar
npm run build

# 2. Restaurar o placeholder __BACKEND_URL__ no web.config (cada aluno edita o seu)
Copy-Item (Join-Path $fe "public\web.config") (Join-Path $fe "dist\web.config") -Force

# 3. Preparar staging limpo
if (Test-Path $stg) { Remove-Item $stg -Recurse -Force }
New-Item -ItemType Directory -Path "$stg\fifa2026-api","$stg\fifa2026-web" | Out-Null

# 4. Frontend pronto -> staging
Copy-Item "$fe\dist\*" "$stg\fifa2026-web" -Recurse -Force

# 5. Backend: copiar fonte (SEM node_modules, .env, logs) e instalar deps de produção
$api = Join-Path $base "fifa2026-api"
foreach ($i in @("src","database","package.json","package-lock.json","web.config",".env.example","README.md")) {
  Copy-Item (Join-Path $api $i) "$stg\fifa2026-api" -Recurse -Force
}
Set-Location "$stg\fifa2026-api"
npm ci --omit=dev    # node_modules apenas de produção (sem nodemon)

# 6. Empacotar os dois ZIPs
Add-Type -AssemblyName System.IO.Compression.FileSystem
$lvl = [System.IO.Compression.CompressionLevel]::Optimal
foreach ($n in @("fifa2026-api","fifa2026-web")) {
  $zip = "$stg\$n.zip"
  if (Test-Path $zip) { Remove-Item $zip -Force }
  [System.IO.Compression.ZipFile]::CreateFromDirectory("$stg\$n", $zip, $lvl, $true)
}
Get-ChildItem $stg -Filter *.zip
```

**Saída:** `_pacote-alunos\fifa2026-api.zip` e `_pacote-alunos\fifa2026-web.zip`.

Distribua aos alunos **3 itens**:

| Arquivo | Vai para | Conteúdo |
|---|---|---|
| `fifa2026-api.zip` | VM-Back | código + `node_modules` (pronto) + `web.config` + `.env.example` |
| `fifa2026-web.zip` | VM-Front | site estático buildado + `web.config` com placeholder |
| `FIFA2026Tickets.bacpac` | VM-DB | banco com dados (fonte da verdade) — baixado do Blob, **não** do repo |

> **Importante:** o ZIP **não** contém `.env` (só `.env.example`). Nenhuma credencial
> real é distribuída — cada aluno cria o próprio `.env`.

### Publicar os ZIPs no Blob Storage

Os dois ZIPs ficam num **container público** (leitura anônima), de onde os alunos baixam direto na VM. Container atual: **`stotfteccopaazure` / `copa2026`**.

```powershell
# Account key (o upload de dados exige key — "--auth-mode login" não basta nesta conta)
$key = az storage account keys list --account-name stotfteccopaazure --query "[0].value" -o tsv
foreach ($f in "fifa2026-api.zip","fifa2026-web.zip") {
  az storage blob upload --account-name stotfteccopaazure --container-name copa2026 `
    --name $f --file "$stg\$f" --content-type application/zip `
    --account-key $key --overwrite --only-show-errors -o none
}
```

URLs públicas (já referenciadas no `GUIA-EVENTO.md`, `GUIA-EVENTO-VMS.md` e no `DEPLOY_IIS_SIMPLIFICADO.md`):

| Artefato | URL |
|---|---|
| Backend | `https://stotfteccopaazure.blob.core.windows.net/copa2026/fifa2026-api.zip` |
| Frontend | `https://stotfteccopaazure.blob.core.windows.net/copa2026/fifa2026-web.zip` |
| Banco (bacpac) | `https://stotfteccopaazure.blob.core.windows.net/copa2026/FIFA2026Tickets.bacpac` |

> ⚠️ Ao **regenerar** os ZIPs (Parte A), refaça o upload com `--overwrite` — senão os alunos baixam a versão antiga.

### Gerar e publicar o `.bacpac` (separado do build dos ZIPs)

O bacpac **não** sai do build local — ele é **exportado do banco vivo** `fifa2026-sql` (fonte da verdade) e publicado no **mesmo container público** `copa2026`. O arquivo full de backup fica também no container privado `copa2026-db`.

```powershell
$key = az storage account keys list --account-name stotfteccopaazure --resource-group RG-DISK-DESAFIO --query "[0].value" -o tsv
# 1. Export do banco vivo -> container privado (backup)
az sql db export --resource-group fifa2026-rg --server fifa2026-sql --name FIFA2026Tickets `
  --admin-user fifa2026admin --admin-password '<DB_PASSWORD>' `
  --storage-key-type StorageAccessKey --storage-key $key `
  --storage-uri "https://stotfteccopaazure.blob.core.windows.net/copa2026-db/FIFA2026Tickets.bacpac"
# 2. Copiar para o container público (download do aluno, igual aos zips)
az storage blob copy start --account-name stotfteccopaazure `
  --destination-container copa2026 --destination-blob FIFA2026Tickets.bacpac `
  --source-container copa2026-db --source-blob FIFA2026Tickets.bacpac --account-key $key
```

> 🔐 **Senha do `fifa2026admin`:** espelhada como app setting `DB_PASSWORD` do Web App `fifa2026-back` (não está no repo). O container `copa2026-db` deve permanecer **privado**; só `copa2026` é público.

---

## PARTE B — Aluno: publicar nas VMs (só configurar)

Topologia (resumo): **VM-Front** (pública, IIS) → `/api/*` via reverse proxy →
**VM-Back** (privada, IIS+iisnode :3001) → **VM-DB** (privada, SQL Server :1433).
Detalhes de rede/NSG em `DEPLOY.md`.

### 1. VM-DB — banco de dados
1. Importar `FIFA2026Tickets.bacpac` no SQL Server (SSMS → *Import Data-tier Application*).
   - Alternativa sem dados: rodar `fifa2026-api/database/schema.sql` + `seed-admin.sql`.
2. Criar login/usuário SQL `fifa2026_db` com acesso ao banco `FIFA2026Tickets`.
3. Garantir SQL em modo de autenticação mista e porta 1433 liberada para a VM-Back.

### 2. VM-Back — backend (privada)
Pré-requisitos na VM: **Node.js 18+**, **IIS**, **iisnode**, **URL Rewrite**.
1. Extrair `fifa2026-api.zip` em `C:\inetpub\wwwroot\fifa2026-api\`.
2. Criar o `.env` a partir do exemplo e ajustar:
   ```powershell
   cd C:\inetpub\wwwroot\fifa2026-api
   Copy-Item .env.example .env
   notepad .env
   ```
   ```env
   DB_SERVER=<IP-privado-da-VM-DB>
   DB_PORT=1433
   DB_USER=fifa2026_db
   DB_PASSWORD=<senha-definida-na-VM-DB>
   DB_NAME=FIFA2026Tickets
   HOST=0.0.0.0
   JWT_SECRET=<string-longa-aleatoria>
   JWT_EXPIRES_IN=7d
   FRONTEND_URL=http://<IP-ou-DNS-publico-da-VM-Front>
   ```
   > `PORT` é injetada pelo iisnode (named pipe) — não precisa setar.
3. No IIS: criar um site/app apontando para a pasta, **Application Pool = No Managed Code**.
4. Testar localmente na VM-Back: `http://localhost:3001/api/health` (ou rota equivalente).

### 3. VM-Front — frontend (pública)
Pré-requisitos na VM: **IIS**, **URL Rewrite** e **ARR** (Application Request Routing,
com *proxy* habilitado em nível de servidor — necessário para o rewrite `/api/*`).
1. Extrair `fifa2026-web.zip` em `C:\inetpub\wwwroot\fifa2026-web\`.
2. Editar **uma linha** do `web.config`: trocar o placeholder pelo IP privado da VM-Back.
   ```powershell
   cd C:\inetpub\wwwroot\fifa2026-web
   (Get-Content web.config) -replace '__BACKEND_URL__','http://10.20.1.5:3001' | Set-Content web.config
   ```
   (substitua `10.20.1.5` pelo IP privado real da sua VM-Back)
3. Apontar o site default (ou um novo site na :80) para essa pasta.
4. Acessar `http://<IP-publico-da-VM-Front>` no navegador.

### Checklist de validação
- [ ] VM-Front abre a home da aplicação.
- [ ] Login funciona (chama `/api/auth/...` → proxy → VM-Back → VM-DB).
- [ ] `web.config` do front **não** contém mais `__BACKEND_URL__`.
- [ ] `.env` do back aponta para o IP correto da VM-DB.

---

## Troubleshooting rápido

| Sintoma | Causa provável | Correção |
|---|---|---|
| Front abre, mas `/api/*` dá 502/404 | ARR proxy não habilitado, ou IP do back errado | IIS → Server → ARR → *Enable proxy*; conferir `__BACKEND_URL__` substituído |
| Back retorna 500 ao logar | `.env` com DB errado / SQL sem mixed auth | revisar `DB_SERVER/USER/PASSWORD`, liberar :1433 |
| `ERR_MODULE_NOT_FOUND` no back | `node_modules` não veio no zip | regerar pacote (Parte A, passo 5) |
| Rotas SPA (F5 em `/standings`) dão 404 | regra "React Routes" do web.config ausente | reextrair o `web.config` do zip |
| Back **não sobe** — iisnode `HRESULT 0x2` / `subStatus 1002` (`/api/health` cai mesmo com `src\index.js` no lugar) | App Pool sem permissão de **ler a pasta** / **criar `src\logs`** (comum após extrair o zip novo) — ou `node.exe` fora do PATH do IIS | `icacls "C:\inetpub\wwwroot\fifa2026-api" /grant "IIS_IUSRS:(OI)(CI)(M)" /T` + `iisreset`. Se instalou o Node agora, reinicie o IIS/VM (PATH só atualiza após restart) |

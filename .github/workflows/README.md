# GitHub Actions — FIFA 2026 Tickets

Pipeline completa de deploy automatizado no Azure (Topology B — PaaS Web Apps).

## Workflows

| Arquivo | Trigger | O que faz |
|---|---|---|
| `deploy-all.yml` | Manual (1 clique) | Pipeline completa: infra → banco → backend → frontend |
| `provision-infra.yml` | Manual / chamado | Bicep + Access Restrictions |
| `setup-database.yml` | Manual / chamado | Schema + seed + migrations |
| `deploy-backend.yml` | Push main / manual / chamado | Publica API Node.js |
| `deploy-frontend.yml` | Push main / manual / chamado | Build React + publica |

---

## Setup Inicial (fazer uma vez)

### Pré-requisitos

- Conta Azure com permissão de criar recursos
- Azure CLI instalado e logado: `az login`
- Repositório GitHub com permissão de criar secrets

### 1. Configurar OIDC (Service Principal)

```bash
# Clone o repo e entre na pasta
export GITHUB_ORG=<seu-usuario-ou-org>
export GITHUB_REPO=<nome-do-repo>

# Opcional: sobrescrever defaults
export RG=fifa2026-rg
export LOCATION=eastus2

bash infra/scripts/setup-oidc.sh
```

O script imprime exatamente o que copiar. Exemplo de saída:

```
  AZURE_CLIENT_ID         xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  AZURE_TENANT_ID         yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
  AZURE_SUBSCRIPTION_ID   zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz
  SQL_ADMIN_PASSWORD      <sua-senha-forte>
  JWT_SECRET              <32-hex-chars-gerados-pelo-script>
```

### 2. Cadastrar Secrets no GitHub

`Settings → Secrets and variables → Actions → New repository secret`

| Secret | Valor |
|---|---|
| `AZURE_CLIENT_ID` | Client ID do App Registration (saída do script) |
| `AZURE_TENANT_ID` | Tenant ID (saída do script) |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID (saída do script) |
| `SQL_ADMIN_PASSWORD` | Senha forte para o SQL Server |
| `JWT_SECRET` | String aleatória de 32+ caracteres |

### 3. Cadastrar Variável

`Settings → Secrets and variables → Actions → Variables → New repository variable`

| Variável | Valor |
|---|---|
| `USE_OIDC` | `true` |

### 4. Criar Environment

`Settings → Environments → New environment → Nome: production`

Para o evento, não adicione regras de proteção (review obrigatório, etc.).

---

## Primeiro Deploy (do zero)

```
Actions → Deploy All — FIFA 2026 → Run workflow
```

Inputs padrão são suficientes para o evento:

| Campo | Default | Quando mudar |
|---|---|---|
| `naming_prefix` | `fifa2026` | Múltiplos alunos no mesmo subscription |
| `location` | `eastus2` | Mudar região |
| `plan_sku` | `B1` | Carga maior → S1/S2 |
| `sql_sku` | `Basic` | Carga maior → S0/S1 |
| `run_migrations` | `true` | Deixar true para dados reais |
| `skip_infra` | `false` | `true` se infra já existe |
| `skip_database` | `false` | `true` se banco já está populado |

O workflow leva ~8–12 minutos. Ao final, as URLs ficam no Job Summary de cada job.

---

## Deploys Subsequentes

Código muda mas infra já existe:

```
Actions → Deploy All → Run workflow
  skip_infra:    true
  skip_database: true
```

Ou triggers automáticos (push em main):
- Backend: qualquer mudança em `fifa2026-api/`
- Frontend: qualquer mudança em `Lovable/World Cup Tickets Hub/`

---

## Workflows Individuais

### Deploy Backend
```
Actions → Deploy Backend (FIFA 2026 API) → Run workflow
  app_name: fifa2026-back
```

### Deploy Frontend
```
Actions → Deploy Frontend (FIFA 2026 Web) → Run workflow
  backend_url: https://fifa2026-back.azurewebsites.net
  app_name:    fifa2026-web
```

### Reprovisionar Infra
```
Actions → Provision Infrastructure (FIFA 2026) → Run workflow
```

### Reinicializar Banco
```
Actions → Setup Database (FIFA 2026) → Run workflow
  run_migrations: true
  reset_schema:   false  (true apaga e recria tudo — use com cuidado)
```

---

## Autenticação: OIDC vs Publish Profile

| | OIDC (recomendado) | Publish Profile (legado) |
|---|---|---|
| **Secrets** | `AZURE_CLIENT_ID/TENANT/SUBSCRIPTION` | `AZURE_BACKEND_PUBLISH_PROFILE` etc. |
| **Segurança** | Token temporário (~1h) | Credencial de longa duração |
| **Setup** | `bash infra/scripts/setup-oidc.sh` | Download via portal/az CLI |
| **Ativar** | Var `USE_OIDC=true` | Remover variável `USE_OIDC` |
| **`deploy-all.yml`** | Suportado | Não suportado |

Os workflows `deploy-backend.yml` e `deploy-frontend.yml` suportam ambos.
O `deploy-all.yml` e `provision-infra.yml` requerem OIDC.

---

## Estrutura dos Arquivos

```
.github/workflows/
├── deploy-all.yml          ← Pipeline mestre (1 clique)
├── provision-infra.yml     ← Bicep + Access Restrictions
├── setup-database.yml      ← Schema + seed + migrations
├── deploy-backend.yml      ← API Node.js (push/manual/call)
├── deploy-frontend.yml     ← React (push/manual/call)
└── README.md

infra/scripts/
└── setup-oidc.sh           ← Cria Service Principal + Federated Credentials
```

---

## Secrets e Variáveis de Referência

### Secrets (obrigatórios para OIDC)
| Nome | Usado em |
|---|---|
| `AZURE_CLIENT_ID` | Todos (OIDC) |
| `AZURE_TENANT_ID` | Todos (OIDC) |
| `AZURE_SUBSCRIPTION_ID` | Todos (OIDC) |
| `SQL_ADMIN_PASSWORD` | `provision-infra`, `setup-database` |
| `JWT_SECRET` | `provision-infra` |

### Secrets (legado, apenas publish profile)
| Nome | Usado em |
|---|---|
| `AZURE_BACKEND_PUBLISH_PROFILE` | `deploy-backend` (sem OIDC) |
| `AZURE_FRONTEND_PUBLISH_PROFILE` | `deploy-frontend` (sem OIDC) |

### Variables (opcionais)
| Nome | Descrição | Default |
|---|---|---|
| `USE_OIDC` | Ativa OIDC nos deploy workflows | — |
| `BACKEND_APP_NAME` | Override do nome do Web App backend | `fifa2026-back` |
| `FRONTEND_APP_NAME` | Override do nome do Web App frontend | `fifa2026-web` |
| `BACKEND_URL` | Override da URL do backend no build do front | `https://fifa2026-back.azurewebsites.net` |

---

## Fluxo do Pipeline

```
workflow_dispatch
       │
       ▼
┌─────────────────────┐
│ 1. provision-infra  │  Bicep → App Service Plan + 2 Web Apps + SQL
│                     │  → Access Restrictions no backend (deny all, allow front IPs)
└────────┬────────────┘
         │ outputs: backend_url, frontend_url, sql_fqdn
         ▼
┌─────────────────────┐
│ 2. setup-database   │  sqlcmd: schema.sql → seed-admin.sql → migrations/
│                     │  Firewall rule temporária para o runner, removida ao final
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ 3. deploy-backend   │  npm ci --omit=dev → azure/webapps-deploy → fifa2026-back
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ 4. deploy-frontend  │  npm ci → vite build (BACKEND_URL embutida) → deploy dist/
└─────────────────────┘  → fifa2026-web
```

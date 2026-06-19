#!/usr/bin/env bash
# =====================================================
# FIFA 2026 — Configuração OIDC para GitHub Actions
# =====================================================
# Execute este script UMA VEZ no seu ambiente local
# para criar o Service Principal com Federated
# Credentials e liberar acesso ao Azure.
#
# Pré-requisitos:
#   az login
#   az account set --subscription "<sua-subscription>"
#   jq  (apt install jq / brew install jq)
#
# Uso:
#   export GITHUB_ORG=<seu-org-ou-usuario>
#   export GITHUB_REPO=<nome-do-repo>
#   bash infra/scripts/setup-oidc.sh
#
# Ao final, o script imprime os 5 secrets a cadastrar
# no GitHub (Settings → Secrets and variables → Actions).
# =====================================================
set -euo pipefail

# ----- Inputs obrigatórios -----
: "${GITHUB_ORG:?Defina GITHUB_ORG=<seu-org-ou-usuario>}"
: "${GITHUB_REPO:?Defina GITHUB_REPO=<nome-do-repo>}"

APP_NAME="${APP_NAME:-fifa2026-github-actions}"
RG="${RG:-fifa2026-rg}"
LOCATION="${LOCATION:-eastus2}"

# ----- Informações da conta atual -----
echo ">> Verificando conta Azure..."
az account show --output none
SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
SUB_NAME=$(az account show --query name -o tsv)

echo "   Subscription : $SUB_NAME ($SUB_ID)"
echo "   Tenant       : $TENANT_ID"
echo "   GitHub       : ${GITHUB_ORG}/${GITHUB_REPO}"
echo

# ----- 1. App Registration -----
echo ">> 1/5  Criando App Registration: $APP_NAME"
EXISTING_APP_ID=$(az ad app list --display-name "$APP_NAME" --query '[0].appId' -o tsv 2>/dev/null || echo "")

if [ -n "$EXISTING_APP_ID" ] && [ "$EXISTING_APP_ID" != "null" ]; then
  APP_ID="$EXISTING_APP_ID"
  echo "   Já existe. Client ID: $APP_ID"
else
  APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
  echo "   Criado. Client ID: $APP_ID"
fi

# ----- 2. Service Principal -----
echo ">> 2/5  Criando Service Principal..."
EXISTING_SP=$(az ad sp show --id "$APP_ID" --query id -o tsv 2>/dev/null || echo "")

if [ -n "$EXISTING_SP" ] && [ "$EXISTING_SP" != "null" ]; then
  SP_ID="$EXISTING_SP"
  echo "   Já existe. Object ID: $SP_ID"
else
  SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)
  echo "   Criado. Object ID: $SP_ID"
fi

# ----- 3. Federated Credentials -----
echo ">> 3/5  Configurando Federated Credentials..."

create_credential() {
  local NAME="$1"
  local SUBJECT="$2"

  EXISTING=$(az ad app federated-credential list --id "$APP_ID" \
    --query "[?name=='${NAME}'].id" -o tsv 2>/dev/null || echo "")

  if [ -n "$EXISTING" ]; then
    echo "   [skip] $NAME (já existe)"
    return
  fi

  az ad app federated-credential create --id "$APP_ID" --parameters "$(cat <<EOF
{
  "name": "${NAME}",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "${SUBJECT}",
  "description": "GitHub Actions — ${GITHUB_ORG}/${GITHUB_REPO}",
  "audiences": ["api://AzureADTokenExchange"]
}
EOF
)" --output none
  echo "   [ok]   $NAME → $SUBJECT"
}

# Push em main (push/push-triggered workflows)
create_credential "github-branch-main" \
  "repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:refs/heads/main"

# Environment 'production' (workflows com environment: production)
create_credential "github-env-production" \
  "repo:${GITHUB_ORG}/${GITHUB_REPO}:environment:production"

# Pull requests (se necessário no futuro)
create_credential "github-pull-request" \
  "repo:${GITHUB_ORG}/${GITHUB_REPO}:pull_request"

# ----- 4. Resource Group -----
echo ">> 4/5  Garantindo Resource Group: $RG ($LOCATION)"
az group create --name "$RG" --location "$LOCATION" -o none 2>/dev/null || true
RG_ID=$(az group show --name "$RG" --query id -o tsv)

# ----- 5. RBAC: Contributor no Resource Group -----
echo ">> 5/5  Atribuindo role Contributor no Resource Group..."
EXISTING_ROLE=$(az role assignment list \
  --assignee "$SP_ID" \
  --role "Contributor" \
  --scope "$RG_ID" \
  --query '[0].id' -o tsv 2>/dev/null || echo "")

if [ -n "$EXISTING_ROLE" ] && [ "$EXISTING_ROLE" != "null" ]; then
  echo "   Já atribuído."
else
  az role assignment create \
    --assignee-object-id "$SP_ID" \
    --assignee-principal-type ServicePrincipal \
    --role "Contributor" \
    --scope "$RG_ID" \
    -o none
  echo "   Atribuído: Contributor em $RG"
fi

# ----- Gerar JWT_SECRET sugerido -----
JWT_SUGGESTION=$(openssl rand -hex 32 2>/dev/null || python3 -c "import secrets; print(secrets.token_hex(32))")

# ----- Output final -----
echo
echo "========================================================"
echo "  OIDC Configurado com Sucesso!"
echo "========================================================"
echo
echo "Cadastre estes secrets no GitHub:"
echo "  Repo → Settings → Secrets and variables → Actions"
echo
echo "  Nome                    Valor"
echo "  ─────────────────────── ──────────────────────────────────────────"
echo "  AZURE_CLIENT_ID         $APP_ID"
echo "  AZURE_TENANT_ID         $TENANT_ID"
echo "  AZURE_SUBSCRIPTION_ID   $SUB_ID"
echo "  SQL_ADMIN_PASSWORD      <senha-forte-para-o-sql>"
echo "  JWT_SECRET              $JWT_SUGGESTION"
echo
echo "Cadastre esta variável (não é secret):"
echo "  Repo → Settings → Secrets and variables → Actions → Variables"
echo
echo "  Nome        Valor"
echo "  ─────────── ──────"
echo "  USE_OIDC    true"
echo
echo "Crie o Environment 'production':"
echo "  Repo → Settings → Environments → New environment → production"
echo "  (sem regras de proteção obrigatórias para o evento)"
echo
echo "Depois execute: Actions → Deploy All — FIFA 2026 → Run workflow"
echo "========================================================"

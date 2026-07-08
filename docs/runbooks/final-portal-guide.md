# Guia do Aluno — A Grande Final (F5/F6: Chatbot MCP + Flow Visualizer + Blindar) do zero

> **O que você vai construir nesta aula:** as duas últimas fases do FIFA 2026 Tickets, criando **do zero** os recursos novos e plugando-os ao ambiente das Quartas — e ainda **fechando as chaves em claro no Key Vault** (a missão Blindar):
> - **F5 — a Voz:** um **McpServer** (7 ferramentas read-only) atrás do gateway YARP + um **chatbot Gemini** que consulta o estado REAL da Copa por conversa natural. A regra de ouro — "o chatbot nunca escreve no banco" — vale **por construção**, não por roteamento.
> - **F6 — a Visão:** o serviço **FlowEvents** (SignalR + Log Analytics) + o **Flow Visualizer** do frontend, onde uma compra real acende **5 nós** animados, rastreados de ponta a ponta por `correlationId`.
> - **Blindar — o cofre:** os segredos (SQL, Gemini, SignalR e o segredo do gateway) saem do **texto puro** das App Settings e vão para o **Key Vault que já existe**, lidos por uma **Managed Identity**. E a **observabilidade nível-produção** (App Insights + Log Analytics) que já está no ar passa a ser usada de verdade.
>
> **Importante (leia antes de começar):**
> - **Este lab ASSUME as Quartas no ar** (gateway YARP, identidade CIAM + admin workforce, backend v1, SQL). A Final **ADICIONA** dois microsserviços ao MESMO ambiente e **reconfigura** os existentes para lerem segredos do cofre — **não** recria o gateway, a identidade, o SQL nem o Key Vault.
> - **Cada aluno cria TUDO no próprio Azure / GitHub**: seus recursos, com **seus próprios nomes**. Os valores deste guia são **genéricos** (`<sufixo>`, `<seu-rg>`, `<gateway-fqdn>`) — preencha os seus na tabela de convenção.
> - **O seu repositório NÃO é o passo zero.** A infra dos serviços novos e o cofre são criados/configurados **à mão** no Portal (Fases 1–9); criar o **repositório a partir do template (`Use this template`) + GitHub Actions é o ÚLTIMO passo de deploy** ([Fase 11](#fase-11--pr-do-lab--rodar-os-acao-na-ordem)).

> ⚠️ **A regra de ouro do dia:** no F5 o chatbot só tem **sentidos** (7 tools de leitura). Ele **não consegue** executar nenhuma ação — não existe uma ferramenta de escrita para o LLM chamar. Você vai **ver isso ao vivo** na [Fase 12](#fase-12--smokes-e-validação-o-coração-do-lab).

> 🏷️ **Marcação de cada passo (repare nas etiquetas ao longo do guia):**
> - **[já existe]** — o recurso já está no ar; você só **usa/liga** (ex.: o Key Vault `kv-dev-tk-cin-001`, o App Insights, o Log Analytics).
> - **[criar/configurar à mão]** — você cria ou reconfigura no Portal nesta aula.
> - **[débito residual]** — fica como dívida conhecida (não bloqueia o lab); registrado honestamente.

> **Referências:** Story [3.3](../stories/3.3.story.md) · [3.4](../stories/3.4.story.md) · **[ADE-010 (Managed Identity + Key Vault sobre os recursos existentes + observabilidade)](../architecture/ade-010-managed-identity-keyvault.md)** · [ADE-009 (X-Gateway-Key)](../architecture/ade-009-network-secrets-service-identity.md) · [ADE-008 (re-arquitetura da Final)](../architecture/ade-008-final-decommission-n8n.md) · [ADE-004 (gateway issuer-agnóstico)](../architecture/ade-004-gateway-yarp.md) · Guia das Quartas [`quartas-f2-portal-guide.md`](./quartas-f2-portal-guide.md) · Workflow [`lab-a-final.yml`](../../.github/workflows/lab-a-final.yml)

---

## Como as peças se encaixam

Há **duas divisões de trabalho** bem distintas — a mesma lógica das Oitavas/Quartas:

| O quê | Como é feito | Onde |
|---|---|---|
| **INFRA nova + COFRE** (Container Apps McpServer/FlowEvents, SignalR, Managed Identities, **secrets no Key Vault**, App Settings novas do gateway, migração das chaves em claro) | **À mão, no Portal do Azure** | Portal (Fases 1–9) |
| **CÓDIGO + FRONTEND** (imagens dos serviços, rebuild do gateway, bundle do front) | **GitHub Actions** (workflow único `Lab A Final`) | Seu repo do template (Fases 10–11) |

O que muda em relação às Quartas:

| | **Quartas (F2/F3)** | **A Final (F5/F6 + Blindar)** |
|---|---|---|
| Gateway YARP | você criou | **reusado** (rebuild do código p/ o hardening + segredo migrado p/ o cofre) |
| Identidade CIAM + admin | você criou | reusada |
| Backend v1 / SQL | reusado | reusado (segredo do gateway migrado p/ o cofre; SQL-MI é showcase opcional) |
| **Key Vault** `kv-dev-tk-cin-001` | já existe | **reusado** — passa a guardar as chaves em claro |
| **McpServer** (7 tools read-only) | — | **NOVO** — Container App **interno**, atrás do gateway |
| **Chatbot Gemini** | — | **NOVO** — no frontend, chave no **proxy server-side** |
| **FlowEvents** (SignalR + Kusto) | — | **NOVO** — Container App + Azure SignalR + Managed Identity |
| **Flow Visualizer** (`/flow`) | — | **NOVO** — 5 nós animados por `correlationId` |
| **Observabilidade** (App Insights + Log Analytics) | já existe | **reusada de verdade** — tracing por `correlationId`, workbook, alertas |

A regra de ouro da arquitetura: **o Portal cria/configura; os Actions só publicam código.** Nenhum recurso Azure é criado pelo workflow.

> 🟢 **Retro-compatibilidade (regra dura):** nada das Quartas deixa de funcionar. A compra continua a mesma; a Final só **acrescenta** observação (chatbot que lê + visualizador que mostra) e **move segredos para o cofre sem downtime** (o valor não muda, só o lar). A notificação pós-compra é **inline** (dentro da Function Consumer), sem orquestração externa.

> 🔵 **Fluxo em runtime (F5):** front → `POST {gateway}/mcp` (Bearer CIAM) → gateway injeta `X-Entra-OID` + `X-Gateway-Key` → **McpServer** (`tools/list`, `tools/call`) → `SELECT` no SQL. A chave Gemini fica no **proxy** (`{gateway}/llm/gemini/...`), nunca no browser.
> 🔵 **Fluxo em runtime (F6):** compra atravessa Gateway YARP → Function Entry → Service Bus → Function Consumer → SQL; cada hop emite um trace com `correlationId`; o **FlowEvents** lê os traces (Kusto) e empurra por **SignalR** para a rota `/flow`, acendendo os 5 nós.

---

## Convenção de nomes (preencha a SUA)

Reuse os recursos das Quartas e crie os **novos** da Final. Anote os **seus** valores — todas as fases referenciam estes placeholders.

| Recurso | Convenção sugerida | Seu valor |
|---|---|---|
| Resource Group | `<seu-rg>` (reuse das Quartas) | ____________ |
| Container Registry (ACR) | `cr<sufixo>.azurecr.io` (reuse) | ____________ |
| Container Apps Environment | `cae-<sufixo>` (reuse) | ____________ |
| Container App (gateway) | `ca-gateway-<sufixo>` (reuse) | ____________ |
| FQDN do gateway | `<gateway-fqdn>` (das Quartas) | ____________ |
| Frontend Web App | `<seu-frontend>` → `https://<seu-frontend>.azurewebsites.net` (reuse) | ____________ |
| Backend v1 (Web App) | `<seu-backend>` (reuse das Quartas) | ____________ |
| Functions F1 (Function App) | `<suas-functions>` (reuse) | ____________ |
| SQL Server / DB | `<seu-sql-server>` / `FIFA2026Tickets` (reuse) | ____________ |
| **Key Vault** | `kv-dev-tk-cin-001` **[já existe]** — RBAC habilitado `[confirmar no Portal]` | ____________ |
| **Managed Identity (leitura do KV)** | `id-fifa2026-kv-reader` — **NOVO, User-Assigned** `[nome sugerido; confirme]` | ____________ |
| **Container App (McpServer)** | `ca-mcp-<sufixo>` — **NOVO, ingress interno** | ____________ |
| FQDN interno do McpServer | `<mcp-fqdn>` (gerado; termina em `.internal.<domínio-do-cae>`) | ____________ |
| **Container App (FlowEvents)** | `ca-flow-<sufixo>` — **NOVO** | ____________ |
| FQDN do FlowEvents | `<flow-fqdn>` (gerado) | ____________ |
| **Azure SignalR** | `signalr-<sufixo>` — **NOVO, tier Free** | ____________ |
| **Log Analytics Workspace** | `log-dev-tk-cin-001` **[já existe]** (o do App Insights) | ____________ |
| Workspace ID (GUID) do Log Analytics | `<workspace-id>` | ____________ |
| **App Insights** | `appi-dev-tk-cin-001` **[já existe]** | ____________ |

> 💡 **Um único segredo de gateway (`X-Gateway-Key`):** você já gerou um `Gateway__AdminSharedSecret` nas Quartas. Nesta aula ele vira **um único secret no Key Vault** (`gateway-admin-shared-secret`), referenciado por **todos** os lados — quem injeta (gateway) e quem valida (backend, Functions, McpServer). Se não tiver anotado o valor, gere um novo (`openssl rand -hex 24`) e use-o como valor do secret no cofre.

---

## Pré-requisitos (checklist de entrada)

- [ ] Ambiente das **Quartas no ar**: gateway YARP responde `GET /health` = 200; login CIAM funciona; compra v2 grava em `purchases`.
- [ ] ACR (`cr<sufixo>`) e o Container Apps Environment (`cae-<sufixo>`) existentes.
- [ ] **Key Vault `kv-dev-tk-cin-001` [já existe]** e você consegue abri-lo no Portal. *(Você dará a si mesmo acesso de dados na [Fase 1](#fase-1--cofre-e-identidade-managed-identity--key-vault).)*
- [ ] **Chave Gemini** pronta (`GEMINI_API_KEY`) — você a gera na [Fase 0](#fase-0--conta-google--chave-gemini-ai-studio) (conta Google dedicada + AI Studio). Modelo do lab: **`gemini-2.5-flash`** (ver [Apêndice B](#apêndice-b--modelo-gemini-real-vs-comentário)).
- [ ] O valor do `Gateway__AdminSharedSecret` das Quartas anotado (ou um novo gerado).
- [ ] A **connection string do SQL** (`FIFA2026Tickets`) e a **connection string do SignalR** (você cria o SignalR na [Fase 5](#fase-5--azure-signalr-free-service-mode-default)) — vão para o cofre.
- [ ] Repositório NOVO **criado do template** do repo do evento (`Use this template` → **Include all branches**; a branch do lab é `lab-a-final`; ver [Fase 11](#fase-11--pr-do-lab--rodar-os-acao-na-ordem)).

---

## Fase 0 — Conta Google + chave Gemini (AI Studio)

O chatbot da Final (F5) usa o **Google Gemini** para decidir qual das 7 tools chamar. A chave (`GEMINI_API_KEY`) é **parte do provisionamento** — você a gera **agora**, antes de subir qualquer serviço. Ela **nunca** entra no código: vira um **secret no Key Vault** ([Fase 1](#fase-1--cofre-e-identidade-managed-identity--key-vault)) e é usada **só pelo proxy server-side** (McpServer).

### 0.1 — Criar a conta Google dedicada ao lab

1. Abra uma **janela anônima/privada** do navegador (para não colidir com sua conta Google pessoal já logada).
2. Acesse **https://accounts.google.com/signup**.
3. Crie uma **conta Google nova, dedicada ao lab** — ex.: `copa.azure.lab.<suas-iniciais>@gmail.com`.

> 💡 **Por que uma conta dedicada?** para **isolar a cota e o faturamento** do free tier do Gemini — a chave fica presa a essa conta e a um **projeto novo**, sem misturar com sua conta pessoal. (Se o facilitador já mantém um Gmail do lab, dá para usar um alias `+` no e-mail de cadastro, ex.: `gmail-do-lab+final@gmail.com`; mas a isolação de cota que importa vem da **conta/projeto novo** — na dúvida, crie a conta dedicada.)

### 0.2 — Gerar a chave no Google AI Studio

1. Ainda logado **nessa conta**, acesse **https://aistudio.google.com/apikey**.
2. **Aceite os termos** do AI Studio.
3. Clique em **Create API key** → **Create API key in new project**.
4. **Copie** a chave e guarde num lugar seguro (gerenciador de senhas / bloco de notas local). Ela **não** vai para o código.

> 🔒 **A chave é server-side:** o `GEMINI_API_KEY` será usado **apenas pelo PROXY** (McpServer, `/llm/gemini/...`) — **nunca** no browser. O frontend só conhece a URL do proxy (`VITE_LLM_PROXY_URL` = o gateway).

✅ **Checkpoint:** você tem um `GEMINI_API_KEY` guardado **fora do código** (vira um secret no cofre na [Fase 1](#fase-1--cofre-e-identidade-managed-identity--key-vault)) e sabe que o modelo do lab é **`gemini-2.5-flash`** (ver [Apêndice B](#apêndice-b--modelo-gemini-real-vs-comentário)).

---

## Fase 1 — Cofre e identidade: Managed Identity + Key Vault

**A missão Blindar começa aqui.** Em vez de colar as chaves **em claro** nas App Settings (como um lab ingênuo faria), você as guarda no **Key Vault que já existe** (`kv-dev-tk-cin-001`) e deixa uma **Managed Identity** lê-las. **Nada é recriado** — é 100% configuração sobre o que existe. Base técnica: **ADE-010**.

> 🧠 **O modelo (leia antes):** uma **User-Assigned Managed Identity compartilhada, só-leitura**, anexada a todos os apps que leem o cofre. Por que compartilhada (e não uma system-assigned por app)? **Um** único role assignment na vida toda; e ela **sobrevive** quando você recria o McpServer/FlowEvents à mão (uma system-assigned morre com o app → novo objectId → regrant obrigatório). Como **ler segredo é uma operação uniforme**, a granularidade por-app não compra segurança aqui. *(O SQL é a exceção — lá se usa a system-assigned por-app; ver [Apêndice E](#apêndice-e--sql-via-managed-identity-showcaseopcional).)*

### 1.1 — Dar a VOCÊ acesso de DADOS ao cofre **[configurar à mão]**

1. Portal → **Key Vault `kv-dev-tk-cin-001` → Access control (IAM) → `+ Add → Add role assignment`**.
2. **Role** = **`Key Vault Secrets Officer`** → **Members** = **sua própria conta** → **Review + assign**.

> ⚠️ **Gotcha #1 (KV-RBAC):** ser **Owner** do recurso **NÃO** dá acesso ao *data-plane* (ler/criar segredo). Sem a role `Key Vault Secrets Officer` em você, o blade **Secrets** nega **403** mesmo sendo Owner. Owner (management plane) ≠ Secrets Officer (data plane). É o erro nº1 de quem nunca migrou um KV com RBAC.
> `[confirmar no Portal]` que o KV está com **RBAC habilitado** (`enableRbacAuthorization = true` — *access policies* inativas). Se estiver em access-policy, o caminho de IAM abaixo muda.

### 1.2 — Criar a Managed Identity compartilhada de leitura **[criar à mão]**

1. Portal → **Managed Identities → `+ Create`**.
2. **Subscription/RG** = os seus · **Region** = a do CAE · **Name** = **`id-fifa2026-kv-reader`** `[nome sugerido; o owner/facilitador confirma]` → **Review + create → Create**.
3. Na **Overview** da MI, anote o **Resource ID** (guardado só para eventuais **fallbacks CLI opcionais** dos Container Apps — ex.: a nota da **Fase 3.3**; no caminho Portal a MI é selecionada **pelo nome**). *(O backend e as Functions da [Fase 9](#fase-9--migração-sem-downtime-backend--functions-das-quartas--key-vault) **não** usam esta UA — eles leem o cofre pela **própria system-assigned**.)*

### 1.3 — Dar à MI a role de leitura de segredo (escopo = o cofre) **[configurar à mão]**

1. Portal → **Key Vault `kv-dev-tk-cin-001` → Access control (IAM) → `+ Add → Add role assignment`**.
2. **Role** = **`Key Vault Secrets User`** (só **lê** o valor do segredo — não list/set/delete) → **Next**.
3. **Assign access to** = **Managed identity** → **`+ Select members`** → selecione **`id-fifa2026-kv-reader`** → **Review + assign**.
4. **Escopo** = **este KV** (o próprio recurso — menor escopo possível, não a subscription/RG).

> ⚠️ A atribuição de role **NÃO é instantânea** — a propagação leva **alguns minutos**. **Valide antes** de trocar qualquer App Setting.
> 💡 **CLI equivalente — OPCIONAL** (fallback, **só se** o principal não aparecer no seletor do Portal; o caminho principal acima é 100% Portal): `az role assignment create --role "Key Vault Secrets User" --assignee-object-id <objectId-da-MI> --assignee-principal-type ServicePrincipal --scope <resourceId-do-KV>`.

### 1.4 — Criar os secrets no cofre (valor **byte-a-byte**)

**[criar à mão]** Para **cada** chave que hoje iria em claro, crie um secret no KV com o **valor idêntico** ao atual. **Esta fase troca o *lar* do segredo, não o *valor*** — é isso que garante o **zero-downtime** depois.

Portal → **Key Vault `kv-dev-tk-cin-001` → Objects → Secrets → `+ Generate/Import`** → **Upload options = Manual** → **Name** + **Secret value** → **Create**. Repita para cada linha:

| Secret no KV | Valor (origem de hoje) | Quem vai referenciar | App Setting / env var no destino |
|---|---|---|---|
| **`gateway-admin-shared-secret`** | o `X-Gateway-Key` das Quartas (hoje **plaintext** no gateway) | **gateway** (injeta) **+ backend + Functions + McpServer** (validam) | Gateway: `Gateway__AdminSharedSecret` · demais: `GATEWAY_SHARED_SECRET` |
| **`gemini-api-key`** | `GEMINI_API_KEY` (da [Fase 0](#fase-0--conta-google--chave-gemini-ai-studio)) | McpServer | `GEMINI_API_KEY` |
| **`sql-connection-string`** | connection string do SQL em forma **ADO.NET** (hoje com **senha**) | McpServer **+ Function F1** (`.NET SqlClient`) | `SqlConnectionString` |
| **`servicebus-connection-string`** | valor atual de `ServiceBusConnection` da Function F1 (byte-a-byte) | **Function F1** | `ServiceBusConnection` |
| **`backend-sql-password`** | valor atual de `DB_PASSWORD` do backend v1 (byte-a-byte) | **backend v1** (Node) | `DB_PASSWORD` |
| `groq-api-key` / `mistral-api-key` *(opcionais)* | chaves de fallback do chatbot | McpServer | `GROQ_API_KEY` / `MISTRAL_API_KEY` |

> 🧭 **Duas formas do segredo do SQL (não confunda):** o **`sql-connection-string`** é a forma **ADO.NET** (`Server=…;User Id=…;Password=…`) que o **.NET SqlClient** do McpServer e da Function F1 consomem **inteira**. O **backend v1 é Node** e lê a senha em um **campo discreto** (`DB_PASSWORD` — ver `fifa2026-api/src/config/database.js`), por isso referencia o **`backend-sql-password`** (só a senha), **não** o `sql-connection-string`. Mesmo banco, mesma senha, **dois formatos** — cada consumidor pega o que sabe ler.

> 📌 **Ainda faltam dois** — você os cria quando o recurso de origem existir:
> - **`azure-signalr-connection-string`** → logo após criar o SignalR na [Fase 5](#fase-5--azure-signalr-free-service-mode-default).
> - **`appinsights-connection-string`** → na [Fase 13](#fase-13--observabilidade-nível-produção-us0) (observabilidade).
>
> 🟢 **Risco ZERO nesta fase:** ninguém referencia esses secrets ainda. Você só está **populando o cofre**. Nada quebra aqui.

> ⭐ **Ganho estrutural (não é só higiene):** o `gateway-admin-shared-secret` é **UM** secret referenciado pelos **dois** lados — quem **injeta** o `X-Gateway-Key` (o gateway) e quem **valida** (backend, Functions, McpServer). Hoje são App Settings **independentes** que podem **divergir por engano** — e divergir = **401 em toda request** → as Quartas caem. Com **um secret só** no cofre, a igualdade vira **garantia estrutural**, não disciplina manual. O cofre **remove uma classe inteira de falha**.

✅ **Checkpoint:** MI `id-fifa2026-kv-reader` criada; role **`Key Vault Secrets User`** atribuída no KV (propagação confirmada); secrets `gateway-admin-shared-secret`, `gemini-api-key`, `sql-connection-string`, `servicebus-connection-string`, `backend-sql-password` criados com **valor byte-idêntico** ao atual; **ninguém referencia ainda**.

---

## Fase 2 — Container App do McpServer (ingress INTERNO)

O McpServer é um microsserviço .NET 8 que expõe o endpoint **`/mcp`** (Streamable HTTP, JSON-RPC 2.0 pelo SDK oficial). Ele fica **atrás do gateway** — o browser **nunca** o chama direto. O gateway valida o Bearer Entra, injeta `X-Entra-OID` (identidade) e `X-Gateway-Key` (prova de origem), e roteia `/mcp` e `/llm/**` para ele.

Nesta fase você cria o Container App **vazio** (imagem placeholder). A imagem real vem pelo Actions na [Fase 11](#fase-11--pr-do-lab--rodar-os-acao-na-ordem).

### 2.1 Criar o Container App (Basics → Container → Ingress)

Tudo no **[portal.azure.com](https://portal.azure.com)**, na `<sua-subscription>` / `<seu-rg>`.

1. Busca do topo → **Container Apps → `+ Create`**.
2. **Basics:**

   | Campo | Valor | Por quê |
   |---|---|---|
   | **Container app name** | `ca-mcp-<sufixo>` | nome do McpServer (vira a Variable `PHASE05_MCP_APP_NAME`) |
   | **Environment** | `cae-<sufixo>` | o **MESMO** CAE do gateway (só quem está no mesmo CAE alcança um ingress interno) |

   → **Next: Container**.
3. **Container:**

   | Campo | Valor | Por quê |
   |---|---|---|
   | **Use quickstart image** | marcado | o ACR real vem pelo Actions; agora é só um placeholder |
   | **CPU / Memory** | menor preset | suficiente para o workshop |

   → **Next: Ingress**.
4. **Ingress:**

   | Campo | Valor | Por quê |
   |---|---|---|
   | **Ingress** | **Enabled** | o gateway precisa alcançá-lo |
   | **Ingress traffic** | **`Limited to Container Apps Environment`** | ⚠️ **INTERNO** — só o gateway, dentro do mesmo CAE, alcança; **sem endereço público** |
   | **Target port** | **`8080`** | obrigatório (`Dockerfile`: `EXPOSE 8080` + `ASPNETCORE_URLS=http://+:8080`); qualquer outra porta = **502** |

5. **Review + create → Create → Go to resource**.
6. Na **Overview**, copie a **Application Url** — é o seu `<mcp-fqdn>` (um host `*.internal.<região>.azurecontainerapps.io`). É o valor da App Setting `McpServerUrl` do gateway ([Fase 4](#fase-4--app-settings-do-gateway-mcpserverurl--x-gateway-key-via-key-vault)).

> 🔒 **Ingress INTERNO é o ponto de segurança do bloco:** o McpServer não tem endereço público. Só o gateway (mesmo CAE) fala com ele — e só com o `X-Gateway-Key` correto. Um `curl` externo direto no McpServer nem chega.

✅ **Checkpoint:** Container App `ca-mcp-<sufixo>` rodando (placeholder), **ingress interno** (`Limited to Container Apps Environment`) na **porta 8080**, e a **Application Url** (`<mcp-fqdn>`, host `.internal…`) anotada.

---

## Fase 3 — ACR + App Settings do McpServer (via Key Vault)

Em vez de colar `SqlConnectionString`, `GEMINI_API_KEY` e `GATEWAY_SHARED_SECRET` **em claro**, você aponta os secrets do Container App para o **Key Vault** (secrets criados na [Fase 1.4](#14--criar-os-secrets-no-cofre-valor-byte-a-byte)), resolvidos pela **Managed Identity compartilhada**.

### 3.1 Conectar o ACR

1. No Container App `ca-mcp-<sufixo>` → **Settings → Registries → `+ Add`**.
2. **Registry** = `cr<sufixo>.azurecr.io` · **Authentication** = **Admin Credentials** → **Save**.

### 3.2 Anexar a Managed Identity de leitura **PRIMEIRO** **[configurar à mão]**

1. No `ca-mcp-<sufixo>` → **Settings → Identity → User assigned → `+ Add`** → selecione **`id-fifa2026-kv-reader`** → **Add**.

> ⚠️ **Ordem obrigatória (landmine P-4):** a MI tem de estar **anexada ANTES** de criar o secret KV-backed. Se você criar o secret apontando para a identidade antes de anexá-la, o ARM **rejeita** o `identityref` — e a falha pode acontecer **depois** de já mexer no app.

### 3.3 Criar os secrets do Container App como **Key Vault reference** **[configurar à mão]**

No `ca-mcp-<sufixo>` → **Settings → Secrets → `+ Add`** — para cada um, escolha o tipo **"Key Vault reference"**:

| Secret do Container App | Key Vault Secret URI | Identity |
|---|---|---|
| `sql-conn` | `https://kv-dev-tk-cin-001.vault.azure.net/secrets/sql-connection-string` | `id-fifa2026-kv-reader` |
| `gemini-key` | `https://kv-dev-tk-cin-001.vault.azure.net/secrets/gemini-api-key` | `id-fifa2026-kv-reader` |
| `gateway-secret` | `https://kv-dev-tk-cin-001.vault.azure.net/secrets/gateway-admin-shared-secret` | `id-fifa2026-kv-reader` |

Depois, em **Application → Containers → `Edit and deploy` → Environment variables**, aponte cada env var para o secret (**Source = Reference a secret**):

| App Setting | Aponta para (secretref) | Papel |
|---|---|---|
| `SqlConnectionString` | `sql-conn` | as 7 tools fazem `SELECT` parametrizado (Dapper) |
| `GEMINI_API_KEY` | `gemini-key` | injetada pelo **proxy** (`/llm/gemini/...`) — NUNCA no bundle |
| `GATEWAY_SHARED_SECRET` | `gateway-secret` | trava `X-Gateway-Key`: só aceita requests que passaram pelo gateway |

→ **Save → Create**.

> 💡 **CLI equivalente — OPCIONAL** (ADE-010 D4a; fallback **só se preferir CLI** — o caminho principal acima é 100% Portal): `az containerapp secret set -n ca-mcp-<sufixo> -g <seu-rg> --secrets "gemini-key=keyvaultref:https://kv-dev-tk-cin-001.vault.azure.net/secrets/gemini-api-key,identityref:<resourceId-da-id-fifa2026-kv-reader>"`. Repita para `sql-conn` e `gateway-secret`. **A beleza:** o env var **continua** `secretref:` — zero churn; o que muda é o **secret do CA**, de valor inline para KV-backed.

> ⚠️ **Manual (cofre) × workflow (inline) — escolha UM caminho para os sensíveis do McpServer:** o job `mcp-server` do `lab-a-final.yml` também sabe aplicar `SqlConnectionString`/`GEMINI_API_KEY`/`GATEWAY_SHARED_SECRET` como *secretref* **inline**, a partir dos Secrets do seu repo ([Fase 10](#fase-10--seu-repositório-do-template--variablessecrets-consolidados)). Se você **blindou pelo cofre** aqui, **não** deixe o workflow reaplicar esses três (ele sobrescreveria o KV-backed por inline); rode o `mcp-server` uma vez para trocar a **imagem** e **re-aponte** os três para o cofre depois, **[débito residual]** ou mantenha-os só manuais. Para o lab, o caminho **cofre** é o "blindado"; o **inline** é o "simples".

> 🔒 **Chave Gemini no server-side:** o frontend só conhece a URL do **proxy** (`VITE_LLM_PROXY_URL` = o gateway). O McpServer expõe `/llm/{provider}/{*path}`, injeta a `GEMINI_API_KEY` como header e encaminha ao endpoint oficial. Assim a key **nunca** vai para o browser — o próprio workflow tem um guard que falha se qualquer key vazar no bundle.
> 🟢 **Opcionais (fallback/portabilidade):** se quiser oferecer outros provedores, o McpServer também lê `GROQ_API_KEY` e `MISTRAL_API_KEY` (crie os secrets `groq-api-key`/`mistral-api-key` no cofre se for usar). Para o lab, só a Gemini basta.

✅ **Checkpoint:** ACR conectado em **Registries**; MI `id-fifa2026-kv-reader` **anexada** ao McpServer; secrets `sql-conn`/`gemini-key`/`gateway-secret` como **Key Vault reference**; env vars `SqlConnectionString`/`GEMINI_API_KEY`/`GATEWAY_SHARED_SECRET` apontando por `secretref`.

---

## Fase 4 — App Settings do gateway (`McpServerUrl` + X-Gateway-Key via Key Vault)

O gateway já roteia para o McpServer — o `McpServerDestinationConfigFilter` **já existe** no `Program.cs` (Story 2.5, reusado). Você dá a URL real do McpServer **e migra o segredo do gateway (hoje em claro) para o cofre** — o gateway é um recurso **existente** das Quartas, então esta é a **primeira migração in-place**.

### 4.1 `McpServerUrl` **[configurar à mão]**

No Container App do **gateway** (`ca-gateway-<sufixo>`) → **Application → Containers → `Edit and deploy` → Environment variables**:

| App Setting | Valor | Papel |
|---|---|---|
| `McpServerUrl` | `https://<mcp-fqdn>` (Application Url da [Fase 2](#fase-2--container-app-do-mcpserver-ingress-interno)) | o filtro sobrescreve a destination do cluster `mcp-server` |

### 4.2 Migrar o `Gateway__AdminSharedSecret` para o cofre (in-place, sem downtime)

**[configurar à mão]** O gateway é um Container App — mesma forma da [Fase 3.2/3.3](#fase-3--acr--app-settings-do-mcpserver-via-key-vault):

1. **Anexe** a MI: `ca-gateway-<sufixo>` → **Settings → Identity → User assigned → `+ Add`** → `id-fifa2026-kv-reader`.
2. **Secret KV-backed:** **Settings → Secrets → `+ Add`** → `gateway-secret` (se ainda não existir no gateway) → tipo **Key Vault reference** → URI `https://kv-dev-tk-cin-001.vault.azure.net/secrets/gateway-admin-shared-secret` → Identity `id-fifa2026-kv-reader`.
3. **Env var:** `Gateway__AdminSharedSecret` → **Source = Reference a secret** → `gateway-secret`.

> 🟢 **Zero-downtime:** o **valor** resolvido é **idêntico** ao plaintext atual (você copiou byte-a-byte na Fase 1.4). O gateway não percebe a troca — só passou a ler do cofre. **GATE antes de seguir (Container App):** a **nova revisão provisiona Healthy** (não fica *Degraded*/falha ao subir) + `GET /health` = **200** + o smoke retro-compat das Quartas (login CIAM + compra v2) funciona. *(O badge "Key Vault Reference · Resolved" é da tela de Configuration do **App Service/Functions** — [Fase 9](#fase-9--migração-sem-downtime-backend--functions-das-quartas--key-vault); em **Container Apps** a falha de resolução aparece como revisão que **não provisiona**, não como badge.)*

> 🔒 **O P0 que a Final fecha:** a partir do hardening (Story 4.2 / ADE-009), o gateway injeta `X-Gateway-Key` também no cluster `mcp-server`. Um `curl` forjando `X-Entra-OID` direto no McpServer **não tem** o segredo e é rejeitado (401); via gateway, a request carrega o segredo real. Por isso é preciso **rebuildar o gateway** a partir da branch `lab-a-final` (`acao=gateway`, [Fase 11](#fase-11--pr-do-lab--rodar-os-acao-na-ordem)) — a imagem das Quartas ainda não tinha o `mcp-server` no conjunto confiável.
> 🔒 **Duplo underscore:** `Gateway:AdminSharedSecret` na config .NET vira `Gateway__AdminSharedSecret` em env var. Vazio = injeção desligada (retro-compat com labs sem gateway).

> 🔵 **Roteamento e cache (só entendimento):** o gateway roteia `/mcp` e `/llm/{**}` → cluster `mcp-server`. Requisições `POST` **não são cacheadas**. O cache de borda (30s) roda **pós-autenticação** (hardening 4.4): um HIT só é servido depois que o JWT é validado.
> 🔵 **Identidade propagada (idem):** o gateway extrai o claim `oid` do token CIAM e injeta `X-Entra-OID`. As tools **leem** esse header só para **logging mascarado** — **nunca** revalidam o JWT (o gateway é o guardião único).

✅ **Checkpoint:** gateway com `McpServerUrl = https://<mcp-fqdn>` e `Gateway__AdminSharedSecret` resolvendo do **cofre** (nova revisão **Healthy** + `/health` 200), com o smoke das Quartas OK. *(A trava `X-Gateway-Key` no `mcp-server` só fica ativa depois do rebuild `acao=gateway` na Fase 11.)*

---

## Fase 5 — Azure SignalR (Free, Service Mode Default)

O FlowEvents empurra os eventos dos 5 nós para o browser via WebSocket, hospedando um **FlowHub** SignalR. Crie o serviço SignalR primeiro — a connection string dele vira um **secret no cofre** que alimenta o FlowEvents.

1. Portal → **SignalR → `+ Create`**.
2. **Basics:**

   | Campo | Valor | Por quê |
   |---|---|---|
   | **Resource name** | `signalr-<sufixo>` | fonte do secret `azure-signalr-connection-string` |
   | **Region** | a **mesma** do CAE | proximidade com o FlowEvents |
   | **Pricing tier** | **Free** (Free_F1) | 20 conexões simultâneas — suficiente para o workshop |

3. **Review + create → Create → Go to resource**.
4. Em **Settings → Service Mode**, confirme **`Default`** (⚠️ **NÃO** `Serverless`) — o `FlowHub` é hospedado pelo próprio serviço FlowEvents (.NET, `AddAzureSignalR`), que exige o modo **Default**.
5. Em **Settings → CORS**, garanta que o **origin do frontend** (`https://<seu-frontend>.azurewebsites.net`) está permitido (o WebSocket do SignalR usa credentials — **não** pode ser `*`).
6. Em **Keys**, copie a **Connection String**.
7. **[criar à mão]** Volte ao **Key Vault `kv-dev-tk-cin-001` → Secrets → `+ Generate/Import`** e crie o secret **`azure-signalr-connection-string`** com esse valor (o pendente da [Fase 1.4](#14--criar-os-secrets-no-cofre-valor-byte-a-byte)).

> 💡 IaC de referência (não obrigatório aplicar): [`infra/phase-06/signalr.bicep`](../../infra/phase-06/signalr.bicep) declara exatamente esse recurso (Free_F1, ServiceMode=Default, CORS restrito).

✅ **Checkpoint:** SignalR `signalr-<sufixo>` criado, **tier Free**, **Service Mode = Default**, **CORS** com o origin do front, e o secret **`azure-signalr-connection-string`** criado no cofre.

---

## Fase 6 — Container App do FlowEvents (ingress externo + WebSocket)

O FlowEvents é um microsserviço .NET 8 que consulta os traces via Kusto (Log Analytics) e empurra os eventos por SignalR. Diferente do McpServer, ele é **externo** — o front conecta o WebSocket a ele (via gateway). Crie o Container App vazio; a imagem real vem pelo Actions.

1. Portal → **Container Apps → `+ Create`**.
2. **Basics:**

   | Campo | Valor | Por quê |
   |---|---|---|
   | **Container app name** | `ca-flow-<sufixo>` | vira a Variable `PHASE06_FLOW_APP_NAME` |
   | **Environment** | `cae-<sufixo>` | o **MESMO** CAE |

   → **Next: Container**.
3. **Container:** mantenha **Use quickstart image** (a real vem pelo Actions) → **Next: Ingress**.
4. **Ingress:**

   | Campo | Valor | Por quê |
   |---|---|---|
   | **Ingress** | **Enabled** | o front conecta o WebSocket |
   | **Ingress traffic** | **`Accepting traffic from anywhere`** | **externo** (diferente do McpServer) |
   | **Transport** | **`Auto`** | habilita **WebSocket** para o SignalR |
   | **Target port** | **`8080`** | obrigatório (`Dockerfile`: `EXPOSE 8080`); outra porta = 502 |

5. **Review + create → Create → Go to resource**. Anote a **Application Url** = `<flow-fqdn>`.
6. **Conectar o ACR:** **Settings → Registries → `+ Add`** → `cr<sufixo>.azurecr.io` → **Authentication** = **Admin Credentials** → **Save**.

> 💡 IaC de referência: [`infra/phase-06/flow-events-containerapp.yaml`](../../infra/phase-06/flow-events-containerapp.yaml) (ingress external, transport auto, target port 8080, Managed Identity SystemAssigned, scale 0→2).

✅ **Checkpoint:** Container App `ca-flow-<sufixo>` rodando (placeholder), **ingress externo**, **Transport = Auto**, **porta 8080**, **ACR conectado** e a **Application Url** (`<flow-fqdn>`) anotada.

---

## Fase 7 — Managed Identity + Log Analytics Reader + App Settings do FlowEvents

O FlowEvents tem **duas** identidades — e isso ilustra o modelo: a **UA compartilhada** lê o **Key Vault** (segredos), e a **system-assigned própria** lê o **Log Analytics** (telemetria). Planos distintos: "quem lê o cofre" ≠ "quem lê os traces".

### 7.1 Ligar a Managed Identity **System-assigned** (para o Log Analytics) **[configurar à mão]**

1. No `ca-flow-<sufixo>` → **Settings → Identity → System assigned** → **Status = On** → **Save**.

### 7.2 Anexar a UA compartilhada (para o Key Vault) **[configurar à mão]**

1. No `ca-flow-<sufixo>` → **Settings → Identity → User assigned → `+ Add`** → `id-fifa2026-kv-reader` → **Add**.

### 7.3 Dar a role **Log Analytics Reader** à system-assigned (IAM) **[configurar à mão]**

1. Vá ao **Log Analytics Workspace** `log-dev-tk-cin-001` **[já existe]** → **Access control (IAM) → `+ Add → Add role assignment`**.
2. **Role** = **`Log Analytics Reader`** → **Next**.
3. **Assign access to** = **Managed identity** → **`+ Select members`** → selecione a identidade **system-assigned** do `ca-flow-<sufixo>` → **Select** → **Review + assign**.
4. Anote o **Workspace ID** (GUID) do Log Analytics (**Overview** do workspace) → vira `PHASE06_LOG_ANALYTICS_WORKSPACE_ID` ([Fase 10](#fase-10--seu-repositório-do-template--variablessecrets-consolidados)).

> ⚠️ Sem o papel **Log Analytics Reader**, o `LogsQueryClient` recebe **403** e os nós nunca acendem.
> 🧠 **A amarração da aula:** a MI que lê o **Log Analytics** (`Log Analytics Reader`) é **irmã** da MI que lê o **Key Vault** (`Key Vault Secrets User`, Fase 1). Uma identidade gerenciada com uma role *Reader* lendo um recurso gerenciado, **sem segredo**. **Segurança e observabilidade são a mesma disciplina, contada duas vezes.**

### 7.4 App Settings do FlowEvents (SignalR via Key Vault) **[configurar à mão]**

**Secrets KV-backed:** `ca-flow-<sufixo>` → **Settings → Secrets → `+ Add`**:
- `azure-signalr-conn` → tipo **Key Vault reference** → URI `https://kv-dev-tk-cin-001.vault.azure.net/secrets/azure-signalr-connection-string` → Identity `id-fifa2026-kv-reader`.
- `diploma-shared-secret` → tipo **Key Vault reference** → URI `https://kv-dev-tk-cin-001.vault.azure.net/secrets/gateway-admin-shared-secret` (o **MESMO** segredo `gateway-admin-shared-secret` que o gateway/backend/Functions/McpServer usam — [Fase 9](#fase-9--migração-sem-downtime-backend--functions-das-quartas--key-vault)) → Identity `id-fifa2026-kv-reader`.

Depois, em **Application → Containers → `Edit and deploy` → Environment variables**:

| App Setting | Valor | Papel |
|---|---|---|
| `AzureSignalRConnectionString` | **secretref** → `azure-signalr-conn` (Key Vault) | hospeda o FlowHub |
| `LogAnalyticsWorkspaceId` | `<workspace-id>` (Fase 7.3) | qual workspace consultar (Kusto) |
| `FrontendOrigin` | `https://<seu-frontend>.azurewebsites.net` | CORS do SignalR (credentials → não pode ser `*`) |
| `DiplomaSharedSecret` | **secretref** → `diploma-shared-secret` (Key Vault) | arma a validação `X-Diploma-Key` do `/api/flow/diploma-summary` (Emenda MEDIUM-4) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` *(opcional; ver [Fase 13](#fase-13--observabilidade-nível-produção-us0))* | secretref → `appinsights-conn` (Key Vault) | telemetria de borda (no-op se ausente) |

> 🔒 **Nota de escopo (Emenda MEDIUM-4 / ADE-009 v1.1):** o cluster `flow-events` continua **FORA** do `X-Gateway-Key` (recent/timeline/SignalR seguem anônimos, como sempre). A **única** exceção é o endpoint `/api/flow/diploma-summary` (Diploma vivo): o gateway injeta um header **distinto** `X-Diploma-Key` **só nessa rota**, e o `DiplomaSharedSecret` acima (reuso do **mesmo** `gateway-shared-secret` da Fase 9) o valida. **Não** existe `GATEWAY_SHARED_SECRET` no FlowEvents — o header/segredo são de nome próprio, escopados ao Diploma. Vazio (não configurado) = bypass legado (o Diploma segue anônimo, como estava antes da emenda).

> 🔎 **Verificação da trava (esquecível!):** se você **pular** o `DiplomaSharedSecret`, o `ca-flow` **não quebra** — mas emite no **startup** um `WARNING` alto (visível em **Log stream / Console** do Container App): *"DiplomaSharedSecret VAZIO … /api/flow/diploma-summary está ANÔNIMO (bypass legado)"*. **Ausência do warning = trava armada.** Se ele aparecer, o Diploma está exposto anônimo no FQDN direto do `ca-flow` (o débito MEDIUM-4 reabre) — volte e configure o `DiplomaSharedSecret`.

✅ **Checkpoint:** MI **System-assigned = On** + **UA `id-fifa2026-kv-reader` anexada**; role **Log Analytics Reader** atribuída à system-assigned no workspace; **Workspace ID** anotado; `AzureSignalRConnectionString` + `DiplomaSharedSecret` resolvendo do **cofre**, `LogAnalyticsWorkspaceId` + `FrontendOrigin` presentes; **nenhum WARNING de `DiplomaSharedSecret` vazio** no startup do `ca-flow`.

---

## Fase 8 — App Setting do gateway (`FlowEventsUrl`)

O gateway já roteia FlowEvents — o `FlowEventsDestinationConfigFilter` **já existe** (Story 2.6, reusado). Só falta a URL real. No gateway `ca-gateway-<sufixo>` → **Environment variables**:

| App Setting | Valor | Papel |
|---|---|---|
| `FlowEventsUrl` | `https://<flow-fqdn>` ([Fase 6](#fase-6--container-app-do-flowevents-ingress-externo--websocket)) | o filtro sobrescreve a destination do cluster `flow-events` |

O gateway expõe duas rotas para o front:
- `/flow-events/api/{**}` → API do FlowEvents (`/api/flow/recent`, `/{id}`, `/{id}/replay`);
- `/flow-events/hubs/{**}` → o Hub SignalR (WebSocket).

> 🔵 O gateway continua o **NÓ ZERO**: injeta `X-Correlation-ID` (transform global) também nas requests ao FlowEvents.

✅ **Checkpoint:** gateway com `FlowEventsUrl = https://<flow-fqdn>`. *(Como o `McpServerUrl`, passa a ser lido pela imagem após o rebuild `acao=gateway` da Fase 11.)*

---

## Fase 9 — Migração sem downtime: backend + Functions das Quartas → Key Vault

Fechar no cofre os segredos ainda em claro nos **recursos existentes** das Quartas — o **backend v1** e as **Functions** (o gateway já foi na [Fase 4.2](#42-migrar-o-gateway__adminsharedsecret-para-o-cofre-in-place-sem-downtime)). Tudo **in-place, um recurso/App Setting por vez, sem derrubar as Quartas**: a **system-assigned de cada recurso** (ligada em 9.1) resolve a Key Vault reference por padrão — só troca o **valor** de cada App Setting. Base: [ADE-010](../architecture/ade-010-managed-identity-keyvault.md) (Ordem de migração).

O que sai do claro por subfase (o shared secret é o que **valida** o `X-Gateway-Key`):

| Subfase | Recurso | Segredo que vai pro cofre |
|---|---|---|
| 9.1 | backend v1 **+** Functions | `GATEWAY_SHARED_SECRET` (o shared secret) |
| 9.2 | Function F1 | `SqlConnectionString` + `ServiceBusConnection` |
| 9.3 | backend v1 | `DB_PASSWORD` (a senha do SQL) |

> 🧭 **Aqui a identidade é a *própria* de cada recurso, não a UA compartilhada.** App Service (backend) e Function App **não** deixam escolher no Portal qual identidade resolve a Key Vault reference (`keyVaultReferenceIdentity` só via CLI/ARM) → usa-se a **system-assigned de cada recurso**, o resolvedor padrão ([doc MS](https://learn.microsoft.com/azure/app-service/app-service-key-vault-references): *"references use the app's system-assigned identity by default"*). **Trade-off:** +2 role assignments (backend e Functions ganham `Key Vault Secrets User`) e 2 exceções à narrativa "só a UA lê o cofre" — em troca de **zero terminal**.

> ⚠️ **Forma diferente do Container App:** backend e Functions **não** usam `secretref`. A Key Vault reference é o **próprio valor** do App Setting:
> `GATEWAY_SHARED_SECRET = @Microsoft.KeyVault(SecretUri=https://kv-dev-tk-cin-001.vault.azure.net/secrets/gateway-admin-shared-secret/)`

### 9.1 Ordem in-place, **um recurso por vez** (repetir p/ backend e p/ Functions) **[configurar à mão]**

**Repita o bloco para o backend v1 e depois para as Functions — um de cada vez, sem big-bang.**

1. Recurso → **Settings → Identity → System assigned** → **Status = On** → **Save**.
2. Key Vault `kv-dev-tk-cin-001` → **Access control (IAM) → `+ Add` → Add role assignment**.
3. **Role** = `Key Vault Secrets User` (mesma da [Fase 1.3](#13--dar-à-mi-a-role-de-leitura-de-segredo-escopo--o-cofre-configurar-à-mão)) → **Next**.
4. **Assign access to** = Managed identity → **`+ Select members`** → selecione a **system-assigned do próprio recurso** (backend **ou** Functions) → **Review + assign**.
5. Recurso → **Configuration** → App Setting `GATEWAY_SHARED_SECRET` → troque o valor para `@Microsoft.KeyVault(SecretUri=https://kv-dev-tk-cin-001.vault.azure.net/secrets/gateway-admin-shared-secret/)` → **Save** (restart de segundos).

> ⚠️ A role leva **alguns minutos** pra propagar — só faça o passo 5 depois. A reference resolve pela system-assigned **por padrão**: nada a apontar, **zero terminal** — mas a identidade **precisa existir e ter a role**.

> ✅ **GATE por recurso (não avance sem):** **Configuration** → `GATEWAY_SHARED_SECRET` mostra **Key Vault Reference · Resolved** (verde) · smoke Quartas: login CIAM + compra v2 OK · `POST` sem token → **401**. Se aparecer a **string literal** `@Microsoft.KeyVault(...)`, a reference não resolveu (system-assigned desligada ou sem role). Só então passe ao próximo recurso.

> ⭐ **Agora a igualdade é estrutural:** gateway, backend, Functions **e** McpServer referenciam **o mesmo** secret `gateway-admin-shared-secret`. Não dá mais para um lado divergir do outro por engano.

> 🎓 **E o Diploma:** esse **mesmo** `gateway-admin-shared-secret` também alimenta o `DiplomaSharedSecret` do `ca-flow` ([Fase 7.4](#74-app-settings-do-flowevents-signalr-via-key-vault-configurar-à-mão)) — é o valor que o gateway injeta como `X-Diploma-Key` **só** na rota `/api/flow/diploma-summary` e que o FlowEvents valida (Emenda MEDIUM-4 / ADE-009 v1.1). Um segredo, cinco consumidores — zero divergência.

### 9.2 Os outros segredos da Function F1 **[configurar à mão]**

A **Function F1** (herdada das Oitavas) ainda guarda em claro **dois** segredos: `SqlConnectionString` (com a senha) e `ServiceBusConnection` (uma SAS key). Feche os dois **in-place**, com a **mesma** forma da 9.1 — a system-assigned desta Function **já está ligada e com a role** (passo 9.1), então **sem novo grant**: só troca o **valor** de cada App Setting, um por vez, por uma Key Vault reference (valor byte-idêntico ao atual).

| App Setting da Function | Trocar o valor para | Origem no cofre (Fase 1.4) |
|---|---|---|
| `SqlConnectionString` | `@Microsoft.KeyVault(SecretUri=https://kv-dev-tk-cin-001.vault.azure.net/secrets/sql-connection-string/)` | `sql-connection-string` (ADO.NET) |
| `ServiceBusConnection` | `@Microsoft.KeyVault(SecretUri=https://kv-dev-tk-cin-001.vault.azure.net/secrets/servicebus-connection-string/)` | `servicebus-connection-string` |

**Procedimento (idêntico ao 9.1, um App Setting por vez):** troque **um** App Setting → **Save** (restart de segundos) → passe pelo **GATE** antes de seguir para o próximo.

> ✅ **GATE por App Setting (não avance para o próximo sem ✅):** Portal → Function → **Configuration** → o App Setting que você **acabou de migrar** (`SqlConnectionString` **ou** `ServiceBusConnection`) mostra **"Key Vault Reference"** com status **Resolved** (verde) + **smoke de retro-compat das Oitavas**: uma **compra v2** grava no `purchases` do SQL **e** o nó **Function Consumer** processa a mensagem da fila (as Oitavas continuam vivas). Se o gate falhar, **reverta** aquele App Setting ao valor **em claro byte-idêntico** (guardado na [Fase 1.4](#14--criar-os-secrets-no-cofre-valor-byte-a-byte)) — blast radius = **um** setting.
>
> 🏁 **Fecho da subfase (só depois do segundo):** com **os dois** migrados, confirme que `SqlConnectionString` **e** `ServiceBusConnection` mostram **Resolved** ao mesmo tempo e o smoke das Oitavas segue verde — só então a Function F1 está 100% no cofre.

> **[débito residual]** O `AzureWebJobsStorage` da Function (a **account key** da Storage, em claro) **fica de fora de propósito**: migrar KV reference nesse setting tem ressalvas de **bootstrap do host** (scale controller) e pode **impedir a Function de subir**. O caminho certo é **identity-based connection** (`AzureWebJobsStorage__accountName` + RBAC de Storage), **fora do escopo**. É a única exceção nomeada ao "zero em claro" das Functions.

### 9.3 A senha do backend v1 **[configurar à mão]**

O backend v1 (Node/App Service) ainda tem a senha em claro — o `database.js` lê `DB_PASSWORD` como **campo discreto** (não uma connection string inteira, ver [nota da Fase 1.4](#14--criar-os-secrets-no-cofre-valor-byte-a-byte)). Feche-o **in-place**, reusando o que a 9.1 montou neste mesmo backend (system-assigned ligada e com a role — **sem novo grant**).

> ⚠️ **Antes de migrar, confirme que NÃO há Connection String no backend.** Em **Configuration**, veja que não existe a **Connection String** `DefaultConnection` nem o App Setting `DB_CONNECTION_STRING`. O `database.js` **prioriza** connection string sobre os campos `DB_*` (linhas 7–13: `connectionString || {...}`) — se houver uma, migrar `DB_PASSWORD` é **no-op silencioso** (a senha segue viva dentro da connection string, em claro). Se existir, migre/remova **ela**, não o `DB_PASSWORD`.

1. `DB_PASSWORD` → troque o valor para `@Microsoft.KeyVault(SecretUri=https://kv-dev-tk-cin-001.vault.azure.net/secrets/backend-sql-password/)` (secret da [Fase 1.4](#14--criar-os-secrets-no-cofre-valor-byte-a-byte), senha atual byte-a-byte) → **Save** (restart de segundos).
2. **Deixe como estão** `DB_SERVER` / `DB_PORT` / `DB_USER` / `DB_NAME` — não há segredo neles; **só a senha** vai pro cofre.

> ✅ **GATE (não avance sem ✅):** Portal → backend → **Configuration** → `DB_PASSWORD` mostra **"Key Vault Reference"** com status **Resolved** (verde) + **smoke das Quartas**: o **login admin workforce** funciona e uma rota `/admin/*` responde (o backend conecta no SQL com a senha resolvida do cofre). Se falhar, **reverta** o `DB_PASSWORD` ao valor **em claro byte-idêntico** — blast radius = **um** recurso.

### 9.4 Pontos onde um erro **derruba as Quartas** (vigie)

| # | Risco | Mitigação |
|---|---|---|
| **P-1** | valor divergente do shared secret → **401 em toda request** | **um** secret referenciado por todos; valor **byte-idêntico** na migração |
| **P-2** | typo em `sql-connection-string` / `servicebus-connection-string` / `backend-sql-password` → 500 nas rotas de compra/consulta (ou trigger do Service Bus que não liga) | copiar exato; validar McpServer/Functions/backend **antes** de seguir; **um App Setting por vez** |
| **P-3** | **system-assigned não ligada** ou **sem a role `Key Vault Secrets User`** no cofre → reference não resolve → string literal | ligar **System assigned = On** + atribuir a role à identidade **antes**; exigir status **Resolved** |
| **P-4** | trocar o secret KV-backed do Container App **antes** de anexar a MI → ARM rejeita | anexar MI **primeiro** (Fases 3.2 / 4.2 / 7.2) |
| **P-5** | **rotação** do shared secret **não é atômica** entre apps → janela de 401 | rotação = **manutenção planejada** (restart coordenado dos dois lados), nunca casual |
| **P-6** | **rede do KV** — se `kv-dev-tk-cin-001` tiver firewall/`publicNetworkAccess: Disabled`, os apps não enxergam o cofre | `[confirmar no Portal]` o networking do KV **antes**; provavelmente público+RBAC (default) |

> 🔙 **Reversão (se um gate falhar):** volte o App Setting migrado (`GATEWAY_SHARED_SECRET`, `SqlConnectionString`, `ServiceBusConnection` ou `DB_PASSWORD`) ao valor **inline plaintext** anterior (que você guardou na Fase 1.4) → o app volta ao estado pré-migração. Como é **um recurso/setting por vez**, o blast radius de um erro é **um** serviço, não o sistema.

> **[débito residual]** Com a [Fase 9.3](#93-a-senha-do-backend-v1-configurar-à-mão) a senha do backend v1 **já saiu do claro** (virou o secret `backend-sql-password` no cofre). O que **resta** como débito é **eliminar a senha de vez**: o backend ainda usa **SQL auth** (o `database.js` lê `DB_PASSWORD`, **não** foi convertido para MI). **Tirar a senha do claro (feito) ≠ eliminá-la** — isso é o SQL-MI, o "próximo nível" do [Apêndice E](#apêndice-e--sql-via-managed-identity-showcaseopcional). Sair do plaintext já é o ganho maior; eliminar a senha é showcase.

✅ **Checkpoint:**
- backend **e** Functions com `GATEWAY_SHARED_SECRET` → **Resolved** (cofre).
- Function F1: `SqlConnectionString` + `ServiceBusConnection` → **Resolved** (9.2).
- backend v1: `DB_PASSWORD` → **Resolved** (9.3).
- cada recurso validado com o smoke Quartas/Oitavas.
- **zero valor em claro** na config — exceções nomeadas: `AzureWebJobsStorage` da Function (débito de bootstrap, 9.2) e a senha do SQL (agora **no cofre**, não em claro) até o SQL-MI ([Apêndice E](#apêndice-e--sql-via-managed-identity-showcaseopcional)).
- os 4 lados (gateway/backend/Functions/McpServer) no **mesmo** `gateway-admin-shared-secret` — gateway/McpServer via **UA compartilhada**, backend/Functions via **system-assigned própria**. **Zero terminal**.

---

## Fase 10 — Seu repositório do template + Variables/Secrets consolidados

Toda a infra e o cofre acima foram criados **à mão**. Agora vem a parte do **seu repositório** (criado do template — [Fase 11.1](#111-preparar-o-seu-repositório-tudo-pela-web-do-github)). No **seu repo** → **Settings → Secrets and variables → Actions**. Os **nomes** são **fixos** (iguais para todos); os **valores** são os **seus** (placeholders da convenção).

### O que você preenche (caminho cofre — o desta aula)

No caminho cofre (o das aulas) você preenche só **2 Secrets** + as **Variables**. Os segredos sensíveis já estão no Key Vault (Fases 3/4/7) — **não vão no seu repo**.

**Secrets (só 2):**

| Nome EXATO | Conteúdo | Usada em (ação) |
|---|---|---|
| `AZURE_CREDENTIALS` | JSON do Service Principal com acesso ao RG | mcp-server · gateway · flow-events |
| `AZURE_FRONTEND_PUBLISH_PROFILE` | publish profile do `<seu-frontend>` (**SCM Basic Auth On** *antes* de capturar) | frontend |

**Variables (as 13 da Final):**

| Nome EXATO | Valor (seu) | Usada em (ação) |
|---|---|---|
| `ACR_LOGIN_SERVER` | `cr<sufixo>.azurecr.io` | mcp-server · gateway · flow-events |
| `PHASE02_RESOURCE_GROUP` | `<seu-rg>` | todos os deploys *(fallback interno `rg-hml-tik-cin-001` no YAML)* |
| `PHASE02_CONTAINERAPP_NAME` | `ca-gateway-<sufixo>` (o Container App do gateway das Quartas) | gateway (rebuild) |
| `PHASE05_MCP_APP_NAME` | `ca-mcp-<sufixo>` | mcp-server |
| `PHASE06_FLOW_APP_NAME` | `ca-flow-<sufixo>` | flow-events |
| `PHASE06_LOG_ANALYTICS_WORKSPACE_ID` | `<workspace-id>` | flow-events |
| `PHASE06_FRONTEND_ORIGIN` | `https://<seu-frontend>.azurewebsites.net` | flow-events |
| `FRONTEND_APP_NAME` | `<seu-frontend>` | frontend |
| `VITE_GATEWAY_V2_URL` | `https://<gateway-fqdn>` | frontend (base das rotas `/mcp`, `/llm`) |
| `VITE_LLM_PROXY_URL` | `https://<gateway-fqdn>` | frontend (proxy de LLM = o gateway) |
| `VITE_LLM_PROVIDER` | `gemini` | frontend (provider ativo do chatbot) |
| `VITE_GEMINI_MODEL` *(opcional)* | `gemini-2.5-flash` | frontend (override; default do código já é `gemini-2.5-flash`) |
| `VITE_FLOW_EVENTS_BASE_URL` | `https://<gateway-fqdn>/flow-events` | frontend (rota `/flow`) |

> 🔁 **Aliases (não duplique):** `VITE_GATEWAY_V2_URL` e `GATEWAY_V2_URL` são o **mesmo valor** — o workflow lê `vars.VITE_GATEWAY_V2_URL || vars.GATEWAY_V2_URL`. Basta setar **uma** das duas. O mesmo vale para `VITE_FUNCTION_V2_URL`/`FUNCTION_V2_URL` (ver a nota das Variables herdadas, abaixo).

> 📌 **Modelo real:** o runtime do `gemini.ts` usa **`gemini-2.5-flash`** (o comentário de cabeçalho do arquivo ainda cita `2.0-flash` — inconsistência conhecida e inofensiva; ver [Apêndice B](#apêndice-b--modelo-gemini-real-vs-comentário)). Não precisa mexer no código.

> ⚠️ **+ 8 Variables herdadas das Quartas (recrie no repo NOVO).** A Final acrescenta chatbot + rota `/flow` ao **mesmo** bundle das Quartas (não recria o front); Variables **não migram entre repositórios** ([Fase 11](#fase-11--pr-do-lab--rodar-os-acao-na-ordem) manda criar um repo novo do template). Copie do seu repo das Quartas as Variables que o job `frontend` injeta **além da tabela acima**:
> - **login CIAM + admin:** `VITE_CIAM_AUTHORITY` · `VITE_CIAM_CLIENT_ID` · `VITE_ADMIN_TENANT_ID` · `VITE_ADMIN_CLIENT_ID` · `VITE_ADMIN_SCOPE`
> - **gateway/backend/compra v2:** `GATEWAY_V2_URL` · `BACKEND_URL` · `FUNCTION_V2_URL`
>
> **Se não recriar, o build passa verde mas publica um bundle com login CIAM e compra v2 mortos.** (O workflow aceita o nome das Quartas ou o prefixado: `GATEWAY_V2_URL` **ou** `VITE_GATEWAY_V2_URL`; `FUNCTION_V2_URL` **ou** `VITE_FUNCTION_V2_URL`.)

**Blindou pelo cofre nas Fases 3/4/7? Não preencha nenhum segredo sensível** — o deploy detecta o secret no Container App e preserva a Key Vault reference. Os sensíveis (e as chaves de fallback) só entram no **seu repo** no **caminho inline**:

> 🔀 **Não blindou pelo cofre?** O caminho inline (preencher os segredos sensíveis no seu repo) está no [Apêndice F](#apêndice-f--caminho-inline-só-para-quem-não-blindou-pelo-cofre).

✅ **Checkpoint (caminho cofre):** **2 Secrets** (`AZURE_CREDENTIALS` + `AZURE_FRONTEND_PUBLISH_PROFILE`) + as **13 Variables** da Final + as **8 Variables herdadas** das Quartas, com os nomes EXATOS acima; **nenhum segredo sensível no seu repo** (blindados no cofre — o deploy detecta o secret existente e **preserva** a Key Vault reference). *(Caminho inline: preencha também os sensíveis do [Apêndice F](#apêndice-f--caminho-inline-só-para-quem-não-blindou-pelo-cofre). O job `frontend` tem fail-fast que aborta se `VITE_CIAM_CLIENT_ID` ou `VITE_FUNCTION_V2_URL` estiverem vazios.)*

---

## Fase 11 — PR do lab + rodar os `acao` na ordem

Este é o **último bloco de deploy**: o Actions só **constrói e publica** imagens/código. A infra e o cofre já existem (Fases 1–9).

### 11.1 Preparar o seu repositório (tudo pela web do GitHub)

A branch do lab no repositório do evento (org **TFTEC**) chama-se **`lab-a-final`** — traz o workflow `lab-a-final.yml` + o código do F5/F6 (McpServer só-sentidos, FlowEvents 5 nós). O repo do evento (`TFTEC/copa-azure-final`) é um **repositório template** — você **não forka**, usa **"Use this template"**.

1. No repo do evento → **`Use this template` → Create a new repository** → ⚠️ marque **Include all branches** (sem isso a branch `lab-a-final` **não vem**) → **Owner** = sua conta → **Create repository**.
2. **Habilite o workflow na `main` do seu repositório:** abra um **Pull Request `lab-a-final` → `main`** (base = `main`, compare = `lab-a-final`) **no próprio repositório** e faça o **merge**. Esse PR é o "exercício" da aula — ele faz o `lab-a-final.yml` aparecer no Actions. (Você nunca dá PR no repo da TFTEC.)

> 💡 **Template ≠ fork:** o seu repositório nasce **desacoplado** do repo do evento (sem vínculo de fork) — **zero risco** de abrir PR por engano contra o repo da TFTEC, e o **GitHub Actions já vem habilitado** por padrão (num fork ele viria **desativado** até você aprovar — vantagem didática do template). Por isso o **Include all branches** é obrigatório: é o que traz a `lab-a-final`.

> 🖱️ **Disparo manual apenas:** o workflow só tem `workflow_dispatch` — nada roda até você clicar em **Run workflow** e escolher a ação. Antes do `frontend`, garanta **SCM Basic Auth `On`** no Web App do front e capture o publish profile **depois** disso.

### 11.2 Rodar o workflow — nesta ordem

Sempre em **Actions → "Lab A Final" → Run workflow → branch `main`** (já com o workflow após o merge da 11.1), variando o `acao`. A ordem (a mesma do `tudo`) é **`mcp-server` → `gateway` → `flow-events` → `frontend`**:

1. **`acao = mcp-server`** — `dotnet build/test` do McpServer, build & push da imagem no ACR (`cr<sufixo>.azurecr.io/mcp-server:<sha>`), `az containerapp update --image` (troca o placeholder) e — se você optou pelo caminho **inline** — aplica os App Settings sensíveis como secrets. Se você **blindou pelo cofre** (Fase 3), deixe `PHASE05_SQL_CONNECTION_STRING` vazio e confirme que os secrets `sql-conn`/`gemini-key`/`gateway-secret` continuam **Key Vault reference** — agora **garantido pelo workflow**: com o secret do seu repo vazio, o deploy detecta o `sql-conn` existente e **não sobrescreve** a blindagem.
   > **O que esperar no log:** como o ingress do McpServer é **interno** (sem endereço público), o workflow **não** faz `curl /health` — ele confirma via `az` que a revisão ativa provisionou. O smoke funcional (`tools/list` = 7 via gateway) é o passo manual da [Fase 12](#fase-12--smokes-e-validação-o-coração-do-lab).
2. **`acao = gateway`** — **rebuild do gateway** a partir de `lab-a-final` para pegar o hardening (`X-Gateway-Key` no cluster `mcp-server` + leitura de `FlowEventsUrl`). Troca a imagem; suas App Settings (incluindo a Key Vault reference da Fase 4) permanecem.
   > **O que esperar no log:** step **"[gateway] Smoke test"** → `POST /purchase` sem token = **401** (fail-closed) + `GET /health` = **200**.
3. **`acao = flow-events`** — `dotnet build/test` do FlowEvents, build & push da imagem (`cr<sufixo>.azurecr.io/flow-events:<sha>`), `az containerapp update --image` + aplica `AzureSignalRConnectionString`, `LogAnalyticsWorkspaceId`, `FrontendOrigin`. Se você **blindou pelo cofre** (Fase 7), deixe `PHASE06_SIGNALR_CONNECTION_STRING` vazio: o deploy detecta o secret `azure-signalr-conn` existente e **não sobrescreve** a Key Vault reference (o env var `AzureSignalRConnectionString` continua apontando pra ela).
   > **O que esperar no log:** step **"[flow-events] Smoke test"** → `GET /health` com `.status == "healthy"` (ingress externo, então há `curl` público).
4. **`acao = frontend`** — `npm ci` + `npm run lint` + `vite build` (chatbot **e** rota `/flow` embutidos, com todas as `VITE_*`) + deploy no Web App.
   > **O que esperar no log:** step **"[frontend] Guard"** → `Guard OK — nenhuma key de LLM no bundle`. Se alguma key de LLM aparecer no bundle, o job **falha** de propósito (a key deve ficar só no proxy server-side).

> 🧩 **Origem dos blocos (reuso, não invenção):** `mcp-server` ← `deploy-phase-05.yml`; `gateway` ← `deploy-phase-02.yml` (é onde vive o deploy do Gateway YARP) + smoke fail-closed do `lab-quartas-de-final.yml`; `flow-events` ← `deploy-phase-06.yml`; `frontend` ← fusão dos jobs de front do phase-05 (chatbot + guard) e phase-06 (rota `/flow`); seletor `acao` ← `lab-quartas-de-final.yml`.

✅ **Checkpoint:** quatro jobs verdes na ordem `mcp-server → gateway → flow-events → frontend` (ou um `tudo`); revisões ativas apontando para as imagens `:<sha>`; frontend publicado com chatbot + `/flow`.

---

## Fase 12 — Smokes e validação (o coração do lab)

Com tudo no ar, prove que o lab funciona — e viva o momento didático central (a regra de ouro ao vivo + os 5 nós).

### 12.1 Smoke do McpServer (tools/list = 7)

**Caminho principal (navegador — DevTools do portal, sem terminal):** o McpServer tem ingress **interno**, então a única porta pública é o **gateway** (`/mcp`) — a mesma que o chatbot da [Fase 12.2](#122-chatbot-3-perguntas-em-linguagem-natural) usa. Faça a chamada `tools/list` de dentro do próprio portal:

1. Abra o **portal já logado** (login CIAM feito) e abra o **DevTools → aba Console** (F12).
2. Pegue um **Bearer CIAM válido**: DevTools → aba **Network** → clique em qualquer request autenticada do portal → copie o valor do header **`Authorization`** (sem o prefixo `Bearer `).
3. No **Console**, cole o snippet (troque `<gateway-fqdn>` e `<access-token-CIAM>`). Rode-o **no próprio tab do portal** — mesma origem que o chatbot já usa:

```js
const GW = "<gateway-fqdn>";
const TOKEN = "<access-token-CIAM>";                 // Bearer copiado do header Authorization
const r = await fetch(`https://${GW}/mcp`, {
  method: "POST",
  headers: {
    "Authorization": `Bearer ${TOKEN}`,
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream"
  },
  body: JSON.stringify({ jsonrpc: "2.0", id: 1, method: "tools/list", params: {} })
});
console.log("status:", r.status, "| X-Cache:", r.headers.get("x-cache"));  // 200 e X-Cache != HIT
console.log(await r.text());   // leia: result.tools[] com EXATAMENTE 7 tools, todas readOnly
```

**Espere:** `status 200`, **7 tools** em `result.tools[]` (todas `readOnly`), e **nenhum** `X-Cache: HIT` (POST `/mcp` não é cacheado). *(Se o navegador bloquear por CORS, use a alternativa por terminal abaixo.)*

> 💡 **Mais leve ainda (só olhos):** abra o **chatbot** (12.2) e, na aba **Network** do DevTools, filtre por `mcp` — a chamada `tools/list` que o próprio chatbot faz aparece ali; abra a resposta e confirme as **7 tools**.

<details><summary><strong>Alternativa por terminal (opcional — mesmo request via <code>curl</code>)</strong></summary>

```bash
GW="<gateway-fqdn>"
TOKEN="<access-token-CIAM>"   # cole um Bearer CIAM válido (login no front → DevTools)

# tools/list via gateway → tem de listar EXATAMENTE 7 tools, todas readOnly
curl -s -X POST "https://${GW}/mcp" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' \
  -i | tee mcp-tools.txt
# Espere: 7 tools em result.tools[]; NENHUM cabeçalho X-Cache: HIT (POST /mcp não é cacheado)
```

</details>

As **7 tools** que devem aparecer (todas read-only):

| Tool | O que consulta |
|---|---|
| `consultar_disponibilidade` | disponibilidade e preços de ingressos de uma partida |
| `verificar_ingresso` | se um ingresso/ID é válido + dados da compra |
| `consultar_bracket` | jogos de uma fase do mata-mata (oitavas…final) |
| `consultar_partidas` | partidas com filtros (time, fase, estádio, grupo, data) |
| `consultar_classificacao` | tabela de pontos de um grupo |
| `consultar_time` | dados de uma seleção (grupo, ranking FIFA, código) |
| `consultar_estadio` | dados de um estádio/sede (cidade, capacidade) |

✅ **Checkpoint (AC-2/AC-8):** `tools/list` = **7 tools, todas `readOnly: true`**; `POST /mcp` sem `X-Cache: HIT`; McpServer com ingress **interno** (não responde por URL pública).

### 12.2 Chatbot: 3 perguntas em linguagem natural

Abra o portal e o **chatbot**. Ele descobre as 7 tools via `tools/list` e deixa o **Gemini** decidir qual chamar (function calling, modo `AUTO`). Faça pelo menos **3** perguntas e observe a tool escolhida:

| Você pergunta | Tool que o Gemini chama | Dado real retornado |
|---|---|---|
| *"Quando o Brasil joga?"* | `consultar_partidas` | jogos do Brasil (com placar se já disputado) |
| *"Como está o grupo A?"* | `consultar_classificacao` | tabela de pontos do grupo A |
| *"Me fala do Maracanã"* | `consultar_estadio` | cidade, capacidade, descrição do estádio |

> 🔎 Cada resposta vem do **SQL real** (via `FifaQueryRepository`, só `SELECT`). O chatbot não inventa: ele lê o banco através das tools.

✅ **Checkpoint (AC-3):** ≥3 das 7 tools demonstradas em conversa natural, com dados reais do SQL.

### 12.3 A regra de ouro AO VIVO (o momento central do F5)

Este é o clímax didático do F5. O facilitador pede à turma que tente uma pergunta de **AÇÃO**:

> *"Cria um alerta pra mim quando abrir ingresso VIP."*

E a turma observa, junto: **o chatbot não tem essa ferramenta.** O `tools/list` só expõe 7 tools de **leitura** — não existe nenhuma tool de escrita para o Gemini chamar. Não há vetor de escrita **por construção**.

Pontos a reforçar em sala:
- A "mão" de ação (uma antiga tool de criar alerta) **foi removida** — o McpServer é só "sentidos".
- Não é preciso explicar roteamento por fila/webhook para provar a segurança: **basta olhar a lista de ferramentas**. O que não existe não pode ser chamado.
- ⚠️ **Nuance honesta:** o LLM pode até *responder em texto* algo como "ok, criei o alerta". Isso é **alucinação de texto**, não uma tool call real — **nenhuma escrita ocorre** no banco. Deixe isso explícito: a "promessa" no texto não é uma ação; o único jeito de escrever seria uma tool call, e ela não existe.

✅ **Checkpoint (AC-4/AC-9):** a turma vê que o chatbot não executa ações; o material não menciona nenhuma "mão"/tool de escrita.

### 12.4 A bolinha atravessa 5 nós (o smoke central do F6)

1. Faça uma **compra v2** no portal (login CIAM → comprar um ingresso).
2. Navegue para **`/flow`**.
3. Observe a "bolinha" atravessar **exatamente 5 nós, em < 30s**, com o **mesmo `correlationId`** em cada hop:

| # | Nó | O que acontece |
|---|---|---|
| 0 | **Gateway YARP** | recebe a request, injeta `X-Correlation-ID` (nó zero do tracing) |
| 1 | **Function Entry** | `PurchaseEntryFunction` valida e publica no Service Bus |
| 2 | **Service Bus** | fila `tickets-purchase` (desacopla entrada e processamento) |
| 3 | **Function Consumer** | `PurchaseConsumerFunction` grava no SQL (idempotente) **e emite a notificação pós-compra INLINE** |
| 4 | **SQL** | linha gravada em `purchases.correlation_id` — fim do fluxo |

4. Abra o **Sheet de inspeção** de cada nó e confira o payload / `correlationId`.

### 12.5 A notificação inline (trade-off dos 5 nós)

No nó **Function Consumer** (nó 3), inspecione o payload e localize a **notificação pós-compra**: ela acontece **inline** (log estruturado correlacionado), **dentro** desse nó — **não tem nó próprio**.

> 🔵 **Por que 5 nós e não 6?** A re-arquitetura da Final **removeu a orquestração externa** de pós-compra: a notificação virou uma etapa **inline** da própria Function Consumer. Ganhamos simplicidade (menos peças, menos falhas, menos custo) ao preço de uma perda visual — a notificação não aparece como uma "bolinha" separada. É um trade-off consciente: a observabilidade da notificação vive no log correlacionado do nó Consumer.

✅ **Checkpoint (AC-4/AC-5/AC-8):** 5 nós exatos, `correlationId` ponta-a-ponta em < 30s; a notificação é encontrada **dentro** do nó Function Consumer; **zero** referência a um 6º nó ou a orquestração externa.

---

## Fase 13 — Observabilidade nível-produção (~US$0)

A **mesma** telemetria que acende os 5 nós também dá **observabilidade de produção** — de graça, porque **reusa** o **App Insights** `appi-dev-tk-cin-001` **[já existe]** e o **Log Analytics** `log-dev-tk-cin-001` **[já existe]** que estão no ar desde as fases anteriores. **Nada é recriado.** Base: **ADE-010 (§Observabilidade)**.

> 🟢 **[já existe, só usar]:** os recursos `appi`/`log`; o *wiring* no código (Gateway/McpServer/FlowEvents/Functions **já** inicializam telemetria via `APPLICATIONINSIGHTS_CONNECTION_STRING` — **no-op** se ausente); o `X-Correlation-ID` que o gateway injeta e **propaga** Gateway → Function → Service Bus → Consumer; os logs estruturados (`ILogger`, incluindo a notificação pós-compra inline correlacionada); o Kusto que o FlowEvents já faz via MI (`Log Analytics Reader`).

### 13.1 Ligar a telemetria (App Insights via Key Vault) **[configurar à mão]**

1. No Key Vault, crie o secret **`appinsights-connection-string`** com a **Connection String** do App Insights `appi-dev-tk-cin-001` (Overview do recurso). *(O pendente da [Fase 1.4](#14--criar-os-secrets-no-cofre-valor-byte-a-byte).)*
2. Em **cada** serviço (gateway, McpServer, FlowEvents, Functions), adicione o App Setting `APPLICATIONINSIGHTS_CONNECTION_STRING` como **Key Vault reference** (`appinsights-conn` → o secret acima). *(Container Apps: `secretref` com a **UA compartilhada** — escolhida na tela de Secrets; Functions: `@Microsoft.KeyVault(...)` resolvido pela **system-assigned** já habilitada e com role no cofre na [Fase 9](#fase-9--migração-sem-downtime-backend--functions-das-quartas--key-vault) — **sem** `keyVaultReferenceIdentity`, **sem terminal**.)* ⚠️ **Na Function, se você já habilitou o App Insights pelo Portal**, o `APPLICATIONINSIGHTS_CONNECTION_STRING` **já existe em claro** — **SUBSTITUA** o valor existente pela Key Vault reference (troca **in-place**, exigindo o status **Resolved**), **não** crie um App Setting duplicado.

### 13.2 Ver o tracing ponta-a-ponta por `correlationId` **[usar]**

No Portal → App Insights `appi-dev-tk-cin-001`:
- **Transaction search**: busque o `X-Correlation-ID` de uma compra → veja o trace **Gateway → Function → Service Bus → Consumer** ponta-a-ponta (é o "trace end-to-end" já previsto no AC-11 das Quartas, hoje só em runtime por falta da conn string).
- **Application Map**: topologia viva dos serviços + dependências (SQL, Service Bus, SignalR), com latência/erro por aresta — o "mapa da cidade" da aula.

### 13.3 Workbook da jornada da compra **[criar à mão]**

Crie um **Azure Workbook** no App Insights (US$0) com: **latência por hop** (gateway → function → consumer), **taxa de falha** por serviço, **throughput/backlog do Service Bus**, **saúde do McpServer/gateway** (`/health`, 5xx, cold starts). Base: `requests`/`dependencies`/`traces` correlacionados por `operation_Id` (ligado ao `X-Correlation-ID`).

### 13.4 Alertas úteis a ~US$0 **[criar à mão]**

Azure Monitor → **Alerts → Create alert rule**:
- **5xx no gateway** acima de N/5min (saúde de perímetro).
- **Dead-letter no Service Bus** > 0 (compra travada — sinal de negócio).
- **Latência do chatbot** (dependência do LLM proxy) acima do p95 alvo.

### 13.5 Consulta por Kusto no Portal **[usar]**

Logs do `log-dev-tk-cin-001`:
```kusto
requests | where customDimensions.CorrelationId == "<id>" | order by timestamp asc
traces   | where message has "pós-compra"   // a notificação inline correlacionada
```

> 🧠 **A amarração da aula (de novo):** a MI que lê o **Log Analytics** (`Log Analytics Reader`, a system-assigned do FlowEvents) é **irmã** da MI que lê o **Key Vault** (`Key Vault Secrets User`, a `id-fifa2026-kv-reader`). Segurança e observabilidade são **a mesma disciplina de identidade gerenciada**, contada duas vezes.

> **[débito residual]** Amostragem/retenção sob controle de custo (o free tier tem **teto de ingestão**); **OpenTelemetry pleno** e a correlação do **frontend** (browser/RUM) ficam **nomeados, não construídos** — o alvo aqui é **US$0/Portal**.

✅ **Checkpoint:** `APPLICATIONINSIGHTS_CONNECTION_STRING` ligado (via cofre) nos serviços; trace por `X-Correlation-ID` visível em **Transaction Search**; **Application Map** povoado; **Workbook** da compra criado; alertas **5xx / dead-letter** ativos.

---

## Retrospectiva — o que você construiu (e por quê)

| Missão | O que provou |
|---|---|
| **Voz** (F5, McpServer) | uma IA pode consultar dados reais com segurança — a regra de ouro vale **por construção** (só 7 sentidos, zero escrita) |
| **Visão** (F6, Flow Visualizer) | observabilidade distribuída: uma compra rastreável ponta-a-ponta por `correlationId`, animada em 5 nós |
| **Blindar** (Managed Identity + Key Vault) | as chaves em claro **saíram** para o cofre, lidas por MI; o `X-Gateway-Key` virou **um** secret com igualdade **estrutural**; observabilidade nível-produção a ~US$0 |
| **Simplificar** (re-arquitetura) | menos peças (notificação inline), menos custo, mesma funcionalidade — retro-compatível com Oitavas/Quartas |

## Perguntas para fechar (discussão em turma)

- Por que o McpServer tem **ingress interno** e o FlowEvents **externo**? (guardião único vs. serviço de leitura de telemetria consumido pelo front via gateway)
- Se alguém tentar `curl` direto no McpServer forjando `X-Entra-OID`, o que acontece? (401 — falta o `X-Gateway-Key`)
- Onde está a chave do Gemini? (no cofre, lida pelo proxy server-side via MI; o front só conhece a URL do proxy)
- Por que a **User-Assigned compartilhada** para ler o cofre, mas a **system-assigned por-app** para o SQL? (ler segredo é uniforme → 1 grant que sobrevive à recriação; o SQL exige menor-privilégio por-serviço)
- Por que a notificação pós-compra não tem nó próprio? (trade-off da re-arquitetura: inline no Consumer)

## Quiz de encerramento

Feche a aula com o **quiz** (Google Forms — link fornecido pelo facilitador na sala): 8 perguntas rápidas sobre o que você construiu — MCP, RAG por tool-use, a regra de ouro por construção, Managed Identity + Key Vault, `correlationId`/observabilidade, os 5 nós e a lição de simplificação. Conteúdo-fonte das perguntas: [`docs/workshops/final/QUIZ.md`](../workshops/final/QUIZ.md).

> 🔗 **Link do quiz:** `<informado pelo facilitador>` (o Forms é criado fora do repositório, padrão das Quartas).

---

## Resumo do que você criou nesta aula

| Camada | Recursos / artefatos |
|---|---|
| **Blindar — cofre** | secrets no Key Vault `kv-dev-tk-cin-001` (SQL ADO.NET, senha do backend, Service Bus, Gemini, SignalR, `gateway-admin-shared-secret`) lidos por Managed Identity — **UA compartilhada `id-fifa2026-kv-reader`** nos Container Apps e **system-assigned de cada recurso** no backend/Functions — + migração in-place (gateway/backend/Functions: `Gateway__AdminSharedSecret`/`GATEWAY_SHARED_SECRET`, `SqlConnectionString`, `ServiceBusConnection`, `DB_PASSWORD`) sem downtime |
| F5 — Voz | Container App **McpServer** (ingress interno, 7 tools read-only) + chatbot Gemini (chave no cofre, proxy server-side) |
| F5 — Gateway | App Settings `McpServerUrl` + `Gateway__AdminSharedSecret` (Key Vault reference; X-Gateway-Key no cluster `mcp-server`) |
| F6 — Visão | Container App **FlowEvents** + **Azure SignalR** (Free/Default) + **Managed Identity** (Log Analytics Reader + leitura do KV) |
| F6 — Gateway/Front | App Setting `FlowEventsUrl` + rota `/flow` (`VITE_FLOW_EVENTS_BASE_URL`) |
| **Observabilidade** | App Insights + Log Analytics reusados: tracing por `correlationId`, Application Map, Workbook da compra, alertas 5xx/dead-letter (~US$0) |
| Automação | Seu repo (do template): Variables + Secrets + workflow único **Lab A Final** (`mcp-server`/`gateway`/`flow-events`/`frontend`/`tudo`) |
| Segurança | McpServer só-leitura por construção · chave Gemini nunca no bundle · segredos no Key Vault (MI) · X-Gateway-Key com igualdade estrutural · cache pós-auth |

---

## Apêndice A — Chave Gemini (AI Studio)

> ➡️ **Movido para a [Fase 0 — Conta Google + chave Gemini (AI Studio)](#fase-0--conta-google--chave-gemini-ai-studio)**, agora parte do provisionamento (antes da Fase 1). O passo a passo de criar a conta Google dedicada e gerar a chave está lá.

## Apêndice B — Modelo Gemini: real vs. comentário

- O **runtime** do `gemini.ts` usa `import.meta.env.VITE_GEMINI_MODEL ?? 'gemini-2.5-flash'` — ou seja, **`gemini-2.5-flash`** por default (sobrescrevível pela Variable `VITE_GEMINI_MODEL`).
- O **comentário de cabeçalho** do arquivo ainda menciona `models/gemini-2.0-flash` (o `2.0-flash` saiu do free tier). É uma **inconsistência de documentação pré-existente**, **inofensiva** e **fora do escopo** deste lab corrigir. Para o aluno, o que vale é o modelo real: **`gemini-2.5-flash`**.

## Apêndice C — Troubleshooting F5 (McpServer + chatbot)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| `tools/list` retorna **8** (não 7) | branch não parte do estado pós-Story 3.1 (McpServer só-sentidos) | confirme que `lab-a-final` está baseada em pós-3.1; deve haver **7** `[McpServerTool(..., ReadOnly = true)]` |
| **401** no `POST /mcp` mesmo com Bearer válido | `Gateway__AdminSharedSecret` ≠ `GATEWAY_SHARED_SECRET`, ou gateway não rebuildado | como os dois agora referenciam o **mesmo** secret do cofre (`gateway-admin-shared-secret`), confirme que **ambas as revisões** (gateway e McpServer) subiram **Healthy** e rode `acao=gateway` |
| **502** em `/mcp` | `McpServerUrl` ausente/errado no gateway, ou target port do McpServer ≠ 8080 | `McpServerUrl = https://<mcp-fqdn>` (Fase 4); ingress target port = **8080** |
| McpServer responde por **URL pública** | ingress criado como **External** (deveria ser interno) | recriar/ajustar ingress = **Limited to Container Apps Environment** (Fase 2.1) |
| App Setting mostra a **string literal** `@Microsoft.KeyVault(...)` | reference não resolveu (backend/Functions **sem system-assigned ligada**, ou a identidade **sem a role** no cofre) | ligar **System assigned = On** + atribuir **`Key Vault Secrets User`** à system-assigned do recurso (Fase 9.1); exigir status **Resolved** |
| Secret do Container App não vira **Key Vault reference** | MI não anexada antes (landmine P-4), ou role não propagada | anexar `id-fifa2026-kv-reader` **antes** (Fase 3.2); aguardar a propagação do IAM |
| Chatbot diz "chat indisponível" | `VITE_LLM_PROXY_URL` não setado no build | definir a Variable (= gateway) e re-rodar `acao=frontend` |
| Chatbot **inventa** uma resposta de ação | alucinação de texto do LLM (function calling não é 100% infalível) | reforçar: a "promessa" no texto **não** é uma tool call; nenhuma escrita ocorre — não há tool de escrita |
| `POST /mcp` retorna `X-Cache: HIT` | regressão do fix de cache do gateway | confirmar que a branch inclui o fix (POST não é cacheado) |
| Build do frontend falha no **guard de key** | uma key de LLM apareceu no bundle | a key deve ficar **só** no proxy server-side; remover qualquer uso direto no front |
| Chatbot responde mas sem dados reais | `SqlConnectionString` ausente/errada no McpServer | conferir o App Setting (Fase 3); se KV-backed, a **revisão do McpServer provisiona Healthy** (Container App não tem badge "Resolved" — a falha aparece como revisão que não sobe) |

## Apêndice D — Troubleshooting F6 (FlowEvents + Flow Visualizer)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| Diagrama mostra **6 nós** ou falta o "Gateway YARP" | branch não parte do estado pós-Story 3.1 (5 nós) | confirmar `flowNodes.ts` com **5** entradas; reconstruir `lab-a-final` do commit correto |
| Nós **nunca acendem** / erro 403 nos traces | Managed Identity **system-assigned** sem **Log Analytics Reader** | conceder o papel à system-assigned do `ca-flow-<sufixo>` no workspace (Fase 7.3) |
| Bolinha **para no nó 2** (Service Bus) | Consumer com backlog ou atraso de ingestão do Kusto (segundos) | aguardar; confirmar Function Consumer rodando |
| `correlationId` não aparece em nenhum nó | SignalR desconectado ou `VITE_FLOW_EVENTS_BASE_URL` incorreto | conferir a Variable (= `{gateway}/flow-events`) e a rota `/flow` conectando ao Hub |
| SignalR não conecta (WebSocket) | ingress do FlowEvents sem transport **Auto**, ou CORS sem o origin do front | ingress transport = **Auto** (Fase 6); CORS do SignalR + `FrontendOrigin` com o origin exato |
| **502** em `/flow-events/**` | `FlowEventsUrl` ausente no gateway | definir `FlowEventsUrl = https://<flow-fqdn>` (Fase 8) |
| **`/diploma` dá 401** (Diploma não carrega a telemetria) | `DiplomaSharedSecret` ausente/divergente no `ca-flow` **ou** o front sem Bearer/`VITE_GATEWAY_V2_URL` | conferir o secretref `diploma-shared-secret` resolvendo (Fase 7.4, **mesmo** valor da Fase 9) **e** o `VITE_GATEWAY_V2_URL` no build do front (o Diploma manda `Authorization: Bearer` via gateway — Emenda MEDIUM-4). Vazio no `ca-flow` = bypass legado (Diploma volta a carregar anônimo) |
| SignalR recusa por tier | recurso criado em modo **Serverless** | recriar SignalR em **Service Mode Default** (Fase 5) |
| `AzureSignalRConnectionString` não resolve | secret KV-backed sem a MI anexada / role não propagada | anexar `id-fifa2026-kv-reader` ao FlowEvents (Fase 7.2) + aguardar IAM |
| Aluno procura um **nó de notificação** dedicado | trade-off aceito (5 nós, notificação inline no Consumer) | reforçar didaticamente (Fase 12.5): a notificação está **dentro** do nó Function Consumer |

## Apêndice E — SQL via Managed Identity (showcase/opcional)

> **"Próximo nível" — NÃO está no caminho crítico do lab.** A [Fase 1](#fase-1--cofre-e-identidade-managed-identity--key-vault) já tira a **senha do SQL do texto puro** (ela vai para o cofre). Este apêndice **elimina a senha** — mas exige mais cerimônia e risco, e o **backend v1 segue com senha por retro-compat**. Faça só se sobrar tempo/ambiente. Base: **ADE-010 D5**.

**O código já suporta** — é troca de **string**, não de mecanismo: `PurchaseRepository.cs` e `FifaQueryRepository.cs` ficam **intactos**; o `Microsoft.Data.SqlClient` resolve o token AAD **nativamente** pela keyword `Authentication=`.

**O que muda:** o **valor** do secret `sql-connection-string` no cofre, de `Server=...;User Id=...;Password=...` para:
```
Server=tcp:sql-dev-tk-cin-001.database.windows.net,1433;Database=FIFA2026Tickets;Authentication=Active Directory Managed Identity;Encrypt=True
```
*(Se a MI for User-Assigned, acrescentar `;User Id=<client-id-da-MI>`.)* O **nome** do secret e as referências **não mudam** — só o valor.

**Pré-requisitos (sem eles o SQL-MI FALHA):**
1. **Azure AD admin no SQL Server** `sql-dev-tk-cin-001` (Portal → SQL Server → **Microsoft Entra ID → Set admin**).
2. Rodar `phase-08-contained-users.sql` **conectado COMO esse admin via AAD** (não SQL-auth), no banco `FIFA2026Tickets`, com os placeholders `<mi-*>` substituídos pelos **nomes reais** das MIs (@data-engineer/@devops).
3. As MIs **system-assigned** dos apps (McpServer, Functions) já habilitadas.

> ⚠️ **Menor-privilégio (não use a UA compartilhada no SQL):** cada app usa a **própria system-assigned** → o próprio *contained user* → o próprio papel (McpServer `db_datareader`-**only** vs Functions writer+reader — ADE-008). Uma MI única colapsaria os dois no **mesmo** user e **quebraria a regra de ouro**.
> `[confirmar no Portal — R-6]` quando um app tem **system E user-assigned**, a string `Authentication=Active Directory Managed Identity` **sem** `User Id` pode resolver a identidade **errada**; se ambíguo, usar `User Id=<client-id>` **explícito**.

**Smoke de menor-privilégio:** um `INSERT` via a MID do **McpServer** deve tomar **permissão negada** (ele é `db_datareader`-only).

## Apêndice F — Caminho inline (só para quem NÃO blindou pelo cofre)

> Só precisa disto quem **não** blindou os sensíveis pelo Key Vault (Fases 3/4/7) — ou quem quer ativar as chaves de fallback do chatbot. No caminho cofre (o das aulas), **pule este apêndice** (os sensíveis já vivem no Key Vault; nada a preencher no seu repo).

**Secrets sensíveis do seu repo:**

| Nome EXATO | Conteúdo | Usada em (ação) |
|---|---|---|
| `PHASE05_SQL_CONNECTION_STRING` | connection string ADO.NET do `FIFA2026Tickets` | mcp-server |
| `PHASE06_SIGNALR_CONNECTION_STRING` | connection string do Azure SignalR | flow-events |
| `GEMINI_API_KEY` | sua chave Gemini | mcp-server |
| `GATEWAY_SHARED_SECRET` | **mesmo** valor do `gateway-admin-shared-secret` | mcp-server |
| `GROQ_API_KEY` / `MISTRAL_API_KEY` *(opcionais)* | chaves de fallback | mcp-server |

> ⚠️ **Cofre × workflow (sem refill):** se você blindou os sensíveis pelo Key Vault (Fases 3/4/7), **deixe o secret do seu repo vazio** — o deploy detecta o secret já existente no Container App e **não sobrescreve** a Key Vault reference. Se você **não** blindou pelo cofre, **preencha** o secret do seu repo (caminho inline). Escolha por segredo:

| Secret do seu repo | Job | Se caminho COFRE | Se caminho INLINE |
|---|---|---|---|
| `PHASE05_SQL_CONNECTION_STRING` | `mcp-server` | **pode deixar vazio** — o deploy detecta o secret `sql-conn` já existente no Container App e **não sobrescreve** a Key Vault reference (só garante o env var `secretref:sql-conn`) | preencha |
| `PHASE06_SIGNALR_CONNECTION_STRING` | `flow-events` | **pode deixar vazio** — o deploy detecta o secret `azure-signalr-conn` já existente e **não sobrescreve** a Key Vault reference | preencha |
| `GEMINI_API_KEY` | `mcp-server` | pode deixar **vazio/ausente** (ausência = aviso, não erro); mantenha `gemini-key` como KV ref | preencha |
| `GATEWAY_SHARED_SECRET` | `mcp-server` | pode deixar **vazio/ausente**; mantenha `gateway-secret` como KV ref | preencha |

> Os **quatro** são condicionais: o job só **aborta** (`exit 1`) se o segredo não existir **nem** no seu repo **nem** como secret no Container App. Blindou pelo cofre → deixe vazio; não blindou → preencha. *(A consolidação cofre × inline num único caminho — antes débito residual — foi **resolvida em 2026-07-06**: o deploy detecta o secret existente e não re-exige refill.)*

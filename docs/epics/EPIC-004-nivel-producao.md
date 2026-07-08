# EPIC-004 — "Nível Produção": Missão Blindar + Consolidação Oitavas/Quartas + Capstone da Grande Final (gate ADE-009)

> **Owner:** Morgan (PM) · **Co-autoria:** **Squad AIOX TFTEC** · **Status:** 📝 **DRAFT** · **Created:** 2026-07-01
> **Relação com EPIC-003:** **épico-irmão** (não sucessor) — EPIC-003 "A Final" entrega as **features** da Grande Final (Voz, Visão, Simplificar-n8n, Identidade, materiais); EPIC-004 entrega o **substrato de produção** (segurança + entrega robusta + clímax). **Os dois co-entregam o MESMO evento** (a Grande Final). Não re-escopo o EPIC-003 — apenas referencio.
> **Gate de arquitetura (pré-condição dura, ✅ satisfeito):** [ADE-009 — Topologia de rede fechada, secrets em cofre e identidade de serviço](../architecture/ade-009-network-secrets-service-identity.md) ✅ **Accepted** (v1.0, 2026-07-01). Suas **4 invariantes** são o escopo técnico da Missão Blindar.
> **Target event:** Aula "A Grande Final" — clímax do workshop Living Lab Azure-Native durante a Copa do Mundo FIFA 2026 ("a Copa que você construiu, agora te responde").
> **Contribuição de tempo de aula (aluno):** ~**45–90 min** dentro da sessão unificada da Final (o **ato Blindar** = grants + a **demo-dinheiro** do bypass + a **cerimônia do Diploma**). **O scaffolding de rede pesado é PRÉ-PROVISIONADO pelo instrutor, fora do relógio da aula** (§Dimensionamento — proteger o clímax, plano §9.4).
> **Estimated squad build effort:** ~**36–40h** (6 stories, ver tabela — executável com paralelismo).
> **Source blueprint (carta magna):** plano aprovado da Grande Final (`giggly-mapping-hearth.md`, §4 Blindar · §5 Consolidação · §6 Entrega · §7 estrutura de épicos · §9 decisões-chave · §10 verificação).

---

## Decisão estrutural: por que a camada Capstone & Entrega entra NO EPIC-004 (e não vira EPIC-005)

> `[AUTO-DECISION]` **A camada Capstone & Entrega (Template Repo, `lab.config.json`/variáveis 15→1, `lab-grande-final.yml`, runbook browser-harness, Diploma vivo, diagrama draw.io) entra NO EPIC-004 como uma frente própria (stories 4.5–4.6) — NÃO vira EPIC-005.** Numeração 4.x **proposta**; confirmação de granularidade/numeração é de **@sm/@po** (autoridade de criação/backlog).

**Justificativa (grounded — 5 razões):**

| # | Critério | Capstone DENTRO do EPIC-004 (escolhido) | Capstone como EPIC-005 (rejeitado) |
|---|---|---|---|
| 1 | **Coerência temática** | ✅ **"Nível Produção" = segurança + entrega robusta**, mesmo norte. Matar a classe de bugs de merge de aluno (Template Repo), colapsar **15 vars → 1 campo** (a maior dor do aluno), e um `lab-grande-final.yml` com `acao=check` (Doctor pré-flight) são todos *"levar isto a nível produção"* — o mesmo objetivo da Missão Blindar. | Separar "entrega" de "segurança" trata como orthogonal o que o **nome do épico** ("Nível Produção") funde. |
| 2 | **Ausência de gate próprio** | ✅ A camada Capstone **não tem um novo ADE**. É governada pela **ADE-009** (entrega materializa Inv 2/3), **ADE-003** ("URL nunca hardcoded") e padrões existentes. **Sem novo gate arquitetural ⇒ não é fronteira de épico** — é o **inverso exato** da lógica que fundou o EPIC-003 ("gate próprio = fronteira de épico"). | Um EPIC-005 sem gate próprio seria um épico "administrativo" sem baseline arquitetural que o justifique. |
| 3 | **Amarração ao gate (dependência dura)** | ✅ A entrega **materializa a ADE-009**: `lab.config.json` + **KV references + User-Assigned MI** = **Inv 2** (zero segredo em GitHub/arquivo/log); `lab-grande-final.yml` com **zero `az create`** (instrutor pré-provisiona) = a materialização operacional da **Inv 3** (rede nasce primeiro, CAE imutável). Orfanar a entrega num EPIC-005 a **separaria do seu gate**. | Um EPIC-005 herdaria a ADE-009 "de longe", quebrando a rastreabilidade Art. IV entre invariante e entregável. |
| 4 | **Precedente honesto** | ✅ Materiais/entrega **sempre** foram **story transversal DENTRO do épico**: EPIC-002 → **S2.7**; EPIC-003 → **3.6**. A camada Capstone é o **análogo EPIC-004** disso — só maior (infra de entrega + experiência), então **duas** stories (4.5 entrega, 4.6 experiência) em vez de uma. | Promover a transversal-de-materiais a épico próprio contraria o padrão dos 3 épicos anteriores. |
| 5 | **Dimensionamento contido** | ✅ O risco de "inchar o EPIC-004" é **mitigado na origem**: o scaffolding de rede pesado é **pré-provisionado pelo instrutor** (browser-harness, offline — §9.4), **não** é hands-on nem build de IaC gigante. Sobra Template Repo + config + workflow + Diploma + diagrama = escopo **limitado e coeso**. | O argumento pró-EPIC-005 ("é grande") perde força quando o peso real (rede) está fora do épico por decisão do owner. |

**Contra-argumento reconhecido (honestidade estratégica):** o plano §7 chama a camada Capstone de **"transversal"** (atravessa Oitavas+Quartas+Final — é o "repo-cabine capstone que pluga por cima"). *"Transversal", porém, é característica de **story** (atravessa stories, roda em paralelo), não de **épico*** — exatamente como 2.7 e 3.6 eram transversais e ficaram in-épico. O framing "pluga por cima de Oitavas/Quartas/Final" descreve o **artefato de runtime** (o repo-template), não a **posse do épico**: o repo é **construído uma vez**, no épico que **fecha o arco** (a Final) = EPIC-004.

**Trade-off aceito + válvula de escape para @sm/@po:** se, no draft, a frente Capstone (4.5+4.6) se mostrar pesada demais para caber no EPIC-004, ela **pode** ser destacada num EPIC-005 futuro — mas o **default é in-épico**. (Mesmo padrão de válvula que o EPIC-003 deu ao @sm para dividir/fundir stories.)

---

## Motivação

A Grande Final foi concebida para ser **inesquecível** e de **nível produção** (plano §1). O owner somou, ao longo da concepção, requisitos que **não são features** — são **substrato**: hardening de segurança ("à prova de balas"), **validar e melhorar** o que já foi entregue nas Oitavas/Quartas, e **simplificar o preenchimento de variáveis** (a maior dor dos alunos). Esses três eixos **não cabem no EPIC-003** (que é features: chatbot, visualizer, identidade, materiais) — eles formam o **EPIC-004 "Nível Produção"**, gated pela **ADE-009**.

Hoje, o **perímetro** do sistema é forte e testado (o gateway YARP das Quartas valida JWT dual-issuer, é fail-closed, injeta `X-Correlation-ID`/`X-Entra-OID`, e injeta `X-Gateway-Key` nas rotas admin), **mas a malha interna ("da porta pra dentro") ainda é de laboratório** (ADE-009 §Context, verificado na fonte):

1. **As Functions do v2 são `AuthorizationLevel.Anonymous`** e confiam no header `X-Entra-OID` **sem validar o token** — proteção por **obscuridade de URL**, não isolamento. Um `curl` forjando `X-Entra-OID` **direto na Function** → **200**. É o **P0 do bypass**.
2. **O McpServer confia no header e nunca revalida o JWT**, apoiado em "estar atrás do gateway" — mas **sem IaC versionado** de `ingress: internal`.
3. **O SQL está aberto a "todos os serviços Azure"** (`AllowAllAzureServices`, `0.0.0.0`–`0.0.0.0`) — **qualquer tenant Azure**, não "os serviços do lab".
4. **Segredos de serviço vivem em connection string** (SQL auth, `ServiceBusConnection`).

A **oportunidade** (reúso, não invenção): as Quartas **já resolveram** a confiança serviço-a-serviço para o v1 (`gatewayTrust.js`, `X-Gateway-Key` timing-safe, fail-closed condicionado ao segredo) e o FlowEvents **já usa Managed Identity** (ACR-pull + Log Analytics Reader). A ADE-009 **generaliza** esses dois faróis. Em paralelo, a auditoria @qa (plano §5) apontou **P0/P1** de consolidação (cache-antes-do-auth, testes ausentes, `UseForwardedHeaders`) que separam "sólido" de "produção". E a **entrega** precisa colapsar a dor das **15 variáveis → 1 campo** para escalar em turmas reais.

**Diferenciação vs EPIC-003 (o épico-irmão):**
- **EPIC-003 "A Final" (features):** *o que o aluno constrói e vê* — chatbot (Voz), visualizer (Visão), identidade unificada, materiais. Gate **ADE-008** (remove n8n).
- **EPIC-004 "Nível Produção" (substrato):** *o chão de produção sob as features* — zero-trust interno (Blindar), qualidade de engenharia (Consolidação), entrega sem-atrito (Capstone) e o **Diploma vivo** (o clímax memorável). Gate **ADE-009** (rede/secrets/identidade-de-serviço).

Juntos: **a Copa que você construiu, agora te responde — e está à prova de balas.**

## Escopo (in scope)

Derivado da **ADE-009 (Invariantes 1–4)** + auditoria @qa (plano §5) + modelo de entrega (plano §6). **Nada além disto** (Art. IV).

### Frente A — Missão BLINDAR (ADE-009 Inv 1–4; T1 US$0 + showcase T2 no SQL)

- **Confiança serviço-a-serviço (Inv 1):** generalizar o `X-Gateway-Key` (timing-safe, fail-closed quando configurado, legado quando vazio) às **Functions F1** e ao **McpServer** — estendendo o transform escopado por `ClusterId` do gateway (`functions-f1`, `mcp-server`) e a validação no destino (generaliza `gatewayTrust.js`). **Fecha o P0 do bypass.** O gateway **permanece o único validador de JWT** (ADE-004 preservada).
- **Data-plane sem connection string (Inv 2):** **Managed Identity + Entra ID** para **SQL** (contained users), **Service Bus** (Sender/Receiver), **ACR** (pull) e **Log Analytics** (Reader). **Irredutíveis** (chaves de LLM que não federam com Entra + `GATEWAY_SHARED_SECRET` simétrico) no **Key Vault**, lidos por **KV reference resolvida pela MI**. **Menor-privilégio por serviço** — o **McpServer = `db_datareader`-only** (a regra de ouro do chatbot vira **garantia de RBAC**).
- **Rede fechada como showcase (Inv 3, T2):** **SQL Private Endpoint** + `publicNetworkAccess: Disabled` + VNet + CAE VNet-integrado **em torno do SQL** — **pré-provisionado pelo instrutor** (CAE imutável → VNet+CAE nascem primeiro, FQDN do gateway planejado antes de `VITE_*`/CORS). ~US$7/mês, **derrubar no mesmo dia**.
- **SQL nunca aberto ao cloud inteiro (Inv 4):** **remover `AllowAllAzureServices`** (`0.0.0.0`–`0.0.0.0`, `sql-database.bicep:60-67`); acesso por PE (T2) ou MI-AAD com `publicNetworkAccess` restrito (T1).

### Frente B — CONSOLIDAÇÃO Oitavas + Quartas (auditoria @qa, plano §5 — roteada ao EPIC-004 pela ADE-009 §Consequences)

- **Testes (P1):** integração da **idempotência SQL** real (hoje só e2e manual); **unit do `gatewayTrust.js`** (backend Node = **0 testes** hoje); teste dos **guards fail-closed de startup** (regressão do `common` passaria hoje).
- **Fixes (P1/P2):** **`XCacheMiddleware` cacheia ANTES do auth** (HIT serve status **sem token** por 30s — `Gateway/Program.cs:419-424`: cache é passo 3, auth é passo 4) → mover o cache para **depois do auth**; **`UseForwardedHeaders`** no gateway (o rate-limit hoje vê o IP do ingress, não do cliente); **doc-fixes** (`"3 tools"`→**7**, typo `Ticker/Ticket`); os **5-fixes-do-PRD** viram **checklist/idempotência no workflow**.

### Frente C — CAPSTONE & ENTREGA (plano §6 + §1 Diploma; a frente da decisão estrutural acima)

- **Template Repository** novo (`fifa2026-final-lab`) → **"Use this template"** gera repo **próprio** do aluno (não fork) — **mata a classe de bugs de merge**. Exercício = PR `final/*`→`main` no **próprio** repo. É o **repo-cabine capstone** (pluga por cima de Oitavas/Quartas).
- **Variáveis 15→1 (a maior dor):** um **`lab.config.json`** versionado (só não-secreto) onde o aluno edita **1 linha (`sufixo`)**; o workflow **resolve o arquivo in-run** (`jq`→`$GITHUB_ENV`) e deriva todos os nomes/URLs — **elimina a camada de GitHub Variables**. Único secret: `AZURE_CREDENTIALS` (valor da turma). **Segredos reais vivem no KV** (KV references + User-Assigned MI — Inv 2) → nunca no GitHub/arquivo/log.
- **`lab-grande-final.yml`** (`workflow_dispatch`), **zero `az create`** (a topologia complexa foi pré-provisionada pelo instrutor), mantém **build + unit tests**, imprime **URLs + checklist manual** + **`acao=check`** (Doctor pré-flight).
- **`browser-harness` = instrutor, offline** — runbook de pré-provisionamento (VNet, CAE, KV, MI, SQL PE) + semeadura de segredos no KV **uma vez** (para em auth walls → não escala em CI, por isso é ferramenta de instrutor).
- **Diploma vivo** (o elemento memorável, ~US$0): página React que **renderiza a telemetria real do aluno** (correlation-IDs, região, hora do deploy — **infalsificável**), assinado *"Squad AIOX + você"* → LinkedIn/loop viral.
- **Diagrama draw.io** de **topologia/segurança** (ícones Azure reais) documentando a malha fechada da ADE-009 (VNet, PE, MI, KV, `X-Gateway-Key`).

## Fora de escopo (out of scope)

Reservado para épico posterior, decisão nova, delegado a outro épico, ou explicitamente nomeado-não-construído pela ADE-009:

- **Unificação de identidade base v1 ↔ CIAM (fence `CiamOnly`, `/api/v2/me`):** é **EPIC-003 / Story 3.5** sob a **emenda ADE-007 v1.3** (fence `CiamOnly` + framing base↔CIAM, Accepted 2026-07-01). **Instrumento é ADE-007, NÃO ADE-009** (plano §3). **Apenas referenciado aqui** — não re-escopo o EPIC-003.
- **T3 aspiracional** (Private Endpoint p/ KV+Service Bus, NAT Gateway, Function Private Endpoint): **nomeado, não construído** (ADE-009 §T3; plano §4). É roteiro de "como iria a produção real", não hands-on.
- **Function Private Endpoint / lockdown total de rede como mecanismo T1:** **rejeitado** (ADE-009 Alt 1) — o `X-Gateway-Key` (US$0, versionado, inspecionável, retrocompatível com os labs sem gateway) fecha o bypass; PE de Function é T3.
- **Connection strings no Key Vault** (em vez de MI-AAD): **rejeitado** (ADE-009 Alt 2) — esconde o segredo sem eliminá-lo; KV fica **só para os irredutíveis**.
- **Managed Identity para LLM keys e `GATEWAY_SHARED_SECRET`:** **impossível** (ADE-009 Alt 3) — LLMs 3rd-party não federam com Entra; `GATEWAY_SHARED_SECRET` é simétrico. São irredutíveis → KV.
- **Manter `AllowAllAzureServices` confiando na senha:** **rejeitado** (ADE-009 Alt 4/Inv 4).
- **Construir o T3 pleno como a aula** (VNet em tudo, todos os PEs, NAT): **rejeitado pelo owner** (ADE-009 Alt 5; plano §9.4) — roubaria o clímax.
- **Features da Final** (chatbot/Voz, visualizer/Visão, re-arquitetura n8n, materiais/deck): **são EPIC-003** (3.1–3.6). EPIC-004 é o **substrato**, não as features.
- **Reescrever backend Node em .NET · mobile · i18n · pagamento real · multi-currency** (herdado dos épicos anteriores).

## Restrições firmes (vacas sagradas)

- ✅ **ADE-004 preservada:** o gateway YARP continua o **guardião único do JWT**. Os backends **provam origem** (`X-Gateway-Key`), **não revalidam** o token (ADE-009 Inv 1; §"O que NÃO muda").
- ✅ **ADE-007 preservada e ortogonal:** o encadeamento `oid → X-Entra-OID → entra_oid` (plano de **identidade**) é distinto do `X-Gateway-Key` (plano de **confiança de transporte**). Nenhum toca o outro. *(A emenda v1.3 é do EPIC-003/3.5, não deste épico.)*
- ✅ **ADE-000 preservada:** trocar connection string por MI **não** altera a lógica de INSERT/idempotência (Inv 4) nem o `correlationId` (Inv 5); a DDL dos contained users é **aditiva/idempotente** (Inv 2).
- ✅ **ADE-008 reafirmada:** sem n8n, some o tráfego leste-oeste `n8n → McpServer`; **ADE-002 Inv 4 (tag n8n) = MOOT** (reafirmado pela ADE-009).
- ✅ **Compatibilidade retroativa (gating por segredo vazio):** `GATEWAY_SHARED_SECRET` vazio = trava desligada = **labs sem gateway (Oitavas/F1) seguem `Anonymous` e funcionando**. Segredo configurado = produção fail-closed. **Nenhum branch de código por ambiente.**
- ✅ **Fluxo v1 intocado** (Node/Express + bcrypt+JWT) — v2 é paralelo.
- ✅ **Menor-privilégio é invariante, não sugestão** (ADE-009 Inv 2): o **McpServer = `db_datareader`-only**; Consumer = writer; Entry = **Service Bus Data Sender**, Consumer = **Data Receiver**.
- ✅ **Proteger o clímax (plano §9.4):** o scaffolding de rede é **pré-provisionado pelo instrutor**; o aluno faz os **grants** + a **demo do bypass**. A rede é a **fundação**, não o espetáculo.
- ✅ **Custo ~US$0 no núcleo (T1)** + PE (T2) showcase de ~US$7/mês **derrubado no dia**.

## Pré-condições do épico (pré-flight)

- ⛔ **GATE — ADE-009 Accepted** (✅ satisfeito, v1.0 2026-07-01). `@pm`/`@sm` **não** draftam stories de hardening antes dela existir. Existe e está aceita → **gate liberado**.
- ⛔ **Faróis de reúso existem e verificados** (ADE-009 §Verificação): `gatewayTrust.js` (X-Gateway-Key timing-safe) + `flow-events-containerapp.yaml` (MI ACR-pull + Log Analytics Reader). O padrão a generalizar já está provado em produção.
- ⚠️ **Dependência branda de EPIC-003** (não bloqueante para o núcleo Blindar): o **Diploma vivo (4.6)** consome a telemetria do **F6 visualizer/FlowEvents (EPIC-003 3.4)**; o doc-fix `"3 tools"→7` (4.4) toca a narrativa do **chatbot (EPIC-003 3.3)**. O núcleo (4.1–4.4) roda **em paralelo** ao EPIC-003; o Capstone (4.5–4.6) é o ponto de integração.

## Success criteria

| # | Critério | Verificação |
|---|---|---|
| SC-1 | **Bypass fechado (a demo-dinheiro):** `curl` forjando `X-Entra-OID` **direto na Function** → **401**; a **mesma** request via gateway → **200** | Demo ao vivo (ADE-009 Inv 1 / Validation) |
| SC-2 | **Zero connection string no data-plane:** `grep` de senha/conn-string na config dos serviços = **0**; SQL/Service Bus por **MI-AAD**; irredutíveis (LLM keys, `GATEWAY_SHARED_SECRET`) só no **KV** | `grep` + inspeção de App Settings (ADE-009 Inv 2) |
| SC-3 | **Menor-privilégio provado:** contained user do **McpServer = só `db_datareader`** (escrita impossível); Consumer=writer; Entry=**SB Data Sender**, Consumer=**Data Receiver** | Inspeção de roles/contained users (ADE-009 Inv 2) |
| SC-4 | **SQL fechado:** `AllowAllAzureServices` **removido**; `publicNetworkAccess: Disabled` (T2/PE) ou restrito+MI-AAD (T1) | `sql-database.bicep` + portal (ADE-009 Inv 4) |
| SC-5 | **Consolidação verde:** novos testes (idempotência SQL integração, `gatewayTrust.js`, startup guards) passando; cache HIT **só com auth** (cache-pós-auth); rate-limit vê IP real (`UseForwardedHeaders`) | `npm test`/`dotnet test` + smoke do cache/rate-limit (plano §5/§10) |
| SC-6 | **Entrega sem-atrito:** "Use this template" → repo **próprio**; aluno edita **1 linha** (`sufixo`) + `check` + `tudo`; **nenhum secret no GitHub** (só `AZURE_CREDENTIALS`; reais no KV) | Dry-run do fluxo do aluno (plano §6/§10) |
| SC-7 | **Diploma vivo renderiza números reais** (correlation-IDs, região, hora do deploy) — infalsificável, assinado "Squad AIOX + você" | Demo individual + inspeção da fonte de telemetria |
| SC-8 | **Perímetro preservado:** ADE-004/007/000/003 intactas; gateway segue guardião único do JWT; fluxo v1 e bases das Oitavas/Quartas sem regressão | Regressão + ADE-009 §Validation (8 itens) |
| SC-9 | **Custo T1 ~US$0** + PE (T2) ~US$7/mês derrubado no dia; custo por aluno ≤ US$15 | Azure Cost Management + Budget Alert |

## Stories

> **Executor primário** = executor padrão da task; **@sm/@po** confirmam numeração e granularidade. Slots de branch = **@devops confirma** (padrão AC-1 da 2.11). **Delegações da ADE-009 respeitadas:** DDL dos contained users → **@data-engineer**; provisionamento MI/RBAC/rede/KV → **@devops**.

| # | ID | Título | Branch destino | Estimativa (build) | Executor primário | Quality gate |
|---|---|---|---|---|---|---|
| S1 | **4.1** | **Blindar (data-plane):** Managed Identity + Key Vault — SQL/Service Bus/ACR/Log Analytics via **MI-AAD** (zero connection string); irredutíveis (LLM keys, `GATEWAY_SHARED_SECRET`) no **KV**; **contained users menor-privilégio** (McpServer = `db_datareader`-only) | `phase-08-hardening` (novo; @devops confirma) | **~7h** | **@dev** (Program.cs + `DefaultAzureCredential` + KV refs) · **@data-engineer** (DDL contained users — delegado ADE-009) · **@devops** (MI + RBAC assignment) | **@architect** (ADE-009 Inv 2) |
| S2 | **4.2** | **Blindar (perímetro interno):** `X-Gateway-Key` nas **Functions/McpServer** (fecha o **P0/bypass** — a demo-dinheiro) + **remover `AllowAllAzureServices`** + **SQL Private Endpoint** (showcase T2, pré-provisionado) | `phase-08-hardening` (+ infra PE pré-provisionada) | **~6h** | **@dev** (transform gateway `functions-f1`/`mcp-server` + validação no destino) · **@devops** (bicep firewall + PE/VNet) | **@architect** (ADE-009 Inv 1/3/4) |
| S3 | **4.3** | **Consolidação (testes):** integração da **idempotência SQL** + unit do **`gatewayTrust.js`** (backend Node 0→cobertura) + teste dos **guards fail-closed de startup** | `phase-08-hardening` | **~5h** | **@dev** (implementa testes; + **@qa** desenho de teste) | **@qa** |
| S4 | **4.4** | **Consolidação (fixes):** **`XCacheMiddleware` cache-pós-auth** + **`UseForwardedHeaders`** + doc-fixes (`3`→`7` tools, typo `Ticker/Ticket`) + **5-fixes-PRD→checklist** no workflow | `phase-08-hardening` | **~4h** | **@dev** (pipeline gateway + doc-fixes; + **@devops** checklist do workflow) | **@qa** (+ **@architect** consult no reorder do pipeline — toca o perímetro ADE-004) |
| S5 | **4.5** | **Entrega (Capstone):** **Template Repo `fifa2026-final-lab`** + **`lab.config.json`** (variáveis **15→1**) + **`lab-grande-final.yml`** (zero `az create`, `acao=check` Doctor) + **runbook browser-harness** (instrutor offline) | **repo novo** `fifa2026-final-lab` (Template) + branch `lab-grande-final` (@devops confirma) | **~8h** | **@devops** (Template Repo + workflow + resolução `jq`→`$GITHUB_ENV`) · **@dev** (schema/resolve `lab.config.json`) · **@analyst** (runbook narrativo) | **@architect** (honra ADE-009 Inv 2/3) + **@po** (UX do aluno: 1 campo + 1 secret) |
| S6 | **4.6** | **Capstone (a experiência):** **Diploma vivo** (telemetria real, infalsificável, assinado "Squad AIOX + você") + **diagrama draw.io** de topologia/segurança (ícones Azure reais) | `phase-08-hardening` (Diploma React) + `docs/diagrams/` (draw.io) + surface no Template Repo | **~8h** | **@dev** (Diploma React) · **@ux-design-expert** (design "viral") · **@analyst** (draw.io) | **@pm** (experiência) + **@architect** (fidelidade do diagrama à ADE-009) |

**Total build estimado:** ~**36–40h** de squad (executável com paralelismo — ver dependências).

> `[AUTO-DECISION]` **6 stories (4.1–4.6)** mapeiam as 3 frentes sem over-splitting: Blindar=4.1+4.2, Consolidação=4.3+4.4, Capstone=4.5+4.6. **Opções deixadas a @sm** (autoridade de story): (a) **fundir 4.3+4.4** numa única "Consolidação" se o tamanho combinado não incomodar — o default é **duas** (testes vs fixes têm perfil de risco e gate distintos: 4.3 é net-new sem mudança de comportamento; 4.4 muda comportamento no pipeline do gateway); (b) **dividir 4.1** em 4.1a (MI+RBAC no data-plane) e 4.1b (KV references dos irredutíveis) se o wiring combinado incomodar — default **unificado** (uma mudança de identidade de data-plane, um gate Inv 2); (c) **dividir 4.2** em 4.2a (`X-Gateway-Key` — o P0) e 4.2b (SQL firewall/PE) — default **unificado** (ambos fecham a superfície interna sob um mesmo gate). O owner e @po/@sm decidem se a frente Capstone (4.5+4.6) permanece in-épico ou vira EPIC-005 (ver §Decisão estrutural — default **in-épico**).

### Como a ADE-009 amarra o escopo (invariante → story — rastreabilidade Art. IV)

| ADE-009 | Conteúdo da invariante | Story(ies) que a materializa(m) |
|---|---|---|
| **Inv 1** | Confiança serviço-a-serviço via `X-Gateway-Key` (timing-safe, fail-closed quando configurado) **OU** isolamento de rede; gateway = único validador de JWT | **4.2** (P0 bypass) |
| **Inv 2** | Zero segredo de serviço em config: data-plane via **MI+Entra**; irredutíveis no **KV**; **menor-privilégio por serviço** (McpServer `db_datareader`-only) | **4.1** (núcleo) · consumida por **4.5** (KV+MI na entrega) |
| **Inv 3** | Rede fechada é o alvo, app-layer é o piso; **CAE imutável → VNet+CAE primeiro, FQDN antes de `VITE_*`/CORS** | **4.2** (SQL PE showcase) · **4.5** (workflow zero-`az create` honra o pré-provisionamento e a ordem) |
| **Inv 4** | SQL nunca aberto a "todos os serviços Azure" — remove `AllowAllAzureServices` | **4.2** (bicep) |
| **§Consequences** (rota P0/P1) | Consolidação (cache-antes-do-auth, testes de idempotência/`gatewayTrust`/startup guards, `UseForwardedHeaders`) — **não é invariante**, mas é escopo do EPIC-004 | **4.3** (testes) · **4.4** (fixes) |

**Leitura:** o **escopo técnico do épico = as 4 invariantes da ADE-009 (4.1/4.2) + a Consolidação que a ADE-009 explicitamente roteou (§Consequences → 4.3/4.4) + a camada de entrega/experiência que materializa Inv 2/3 para o aluno (4.5/4.6)**. Cada AC das stories deverá citar a invariante/decisão de origem (Art. IV — @sm/@po no draft).

## Dependências entre stories (e relação com EPIC-003)

```
   ┌─ ADE-009 (GATE, ✅ Accepted) ─┐
   ▼                               │
4.1 (MI+KV data-plane; contained users) ── fundação de segredos
   └─> 4.2 (X-Gateway-Key + SQL firewall/PE)   [o GATEWAY_SHARED_SECRET vive no KV de 4.1; Inv 1 lê do KV]
                                               [mas o transform+timing-safe reusa o padrão provado das Quartas — mecanismo independente]
4.3 (Consolidação testes) ───── paralela (net-new; sem mudança de comportamento)
4.4 (Consolidação fixes) ────── paralela (cache-pós-auth toca o pipeline do gateway → @architect consult)

4.5 (Entrega/Template Repo) ── precisa do núcleo Blindar pronto (KV+MI) + das features do EPIC-003 (é a cabine capstone)
   └─> 4.6 (Diploma + draw.io)  [LAST — renderiza/documenta o estado final: telemetria (EPIC-003 F6) + topologia (4.1/4.2)]

── Relação com EPIC-003 (épico-irmão, mesmo evento) ──
EPIC-003 3.4 (F6 visualizer/FlowEvents) ──► fonte de telemetria do Diploma (4.6)
EPIC-003 3.3 (chatbot/McpServer) ─────────► McpServer recebe X-Gateway-Key (4.2); doc-fix "3→7 tools" (4.4)
EPIC-003 3.1 (remoção n8n) ────────────────► MOOT o tráfego n8n→McpServer (ADE-009 reafirma) — não bloqueia 4.2
ADE-007 v1.3 (fence CiamOnly) ─────────────► EPIC-003 3.5 (NÃO deste épico — só referência)
```

- **4.1 é a fundação de segredos:** o `GATEWAY_SHARED_SECRET` (irredutível) passa a viver no KV via 4.1; 4.2 (Inv 1) o consome de lá. **Mas o mecanismo** do `X-Gateway-Key` (transform + timing-safe) **reusa o padrão já provado das Quartas** — então 4.2 pode ser draftada em paralelo, ancorando só a *fonte* do segredo em 4.1.
- **4.2 é a demo-dinheiro:** o P0 do bypass. É o hands-on de segurança da aula (grants em 4.1 + o `curl` 401/200 em 4.2).
- **4.3/4.4 são paralelas** ao núcleo Blindar (qualidade de engenharia; não bloqueiam o hands-on). **4.4** toca o pipeline do gateway (cache-pós-auth) → **@architect consult** para não ferir o perímetro (ADE-004).
- **4.5 é o ponto de integração:** a cabine capstone só fecha com o núcleo Blindar (KV+MI) pronto **e** as features do EPIC-003 disponíveis (é o repo que empacota a Final inteira).
- **4.6 é o último:** o Diploma **renderiza** o estado final (telemetria do F6/EPIC-003); o draw.io **documenta** a topologia final (4.1/4.2). "Materiais seguem a forma do código" (mesmo princípio do 3.6 no EPIC-003).

## Artefatos pré-existentes que cada story alavanca (reúso > invenção — IDS/Art. IV)

| Story | Reusa (verificado na ADE-009 §Verificação) |
|---|---|
| **4.1** | `flow-events-containerapp.yaml` (farol MI: ACR-pull `identity: system` + Log Analytics Reader); `phase-04-ciam-link.sql` (padrão de DDL aditiva/idempotente para os contained users); os `Program.cs` dos serviços (ponto de wiring do `DefaultAzureCredential`) |
| **4.2** | `gatewayTrust.js` (`X-Gateway-Key` timing-safe, fail-closed condicionado ao segredo — **a generalizar**); `Gateway/Program.cs:118-144` (transform escopado por `ClusterId`, hoje só `backend-v1`, anti-spoofing remove header externo — **a estender** para `functions-f1`/`mcp-server`); `sql-database.bicep` (l.38, 60-67 — comentários já apontam o PE) |
| **4.3** | `PurchaseConsumerFunction` (idempotência `case Inserted` — alvo do teste de integração); `gatewayTrust.js` (alvo dos primeiros unit tests do backend Node); os guards de startup fail-closed do gateway (`common`) |
| **4.4** | `Gateway/Program.cs:419-424` (pipeline: cache=passo 3, auth=passo 4 — **a reordenar**); pipeline do gateway (ponto do `UseForwardedHeaders`); docs do chatbot (`3`→`7` tools); o workflow das Quartas (`lab-quartas-de-final.yml`, padrão `acao`-guardado) como molde do checklist dos 5-fixes-PRD |
| **4.5** | `lab-quartas-de-final.yml` (workflow `acao`-guardado, deploy sem-`az create` — **molde** do `lab-grande-final.yml`); o padrão de branch curada `lab-*` das Oitavas/Quartas; `browser-harness` (skill já instalada — ferramenta de instrutor offline); KV+User-Assigned MI de 4.1 |
| **4.6** | EPIC-003 3.4 (F6: FlowEvents + App Insights + SignalR — fonte de telemetria); frontend React/Vite existente (onde o Diploma vive); pacote validado Oitavas/Quartas (draw.io com ícones Azure reais) |

## Riscos e mitigações

| # | Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|---|
| 1 | **Tempo/fadiga na aula** (mesmo risco que estourou nas Quartas ~7,5–9,5h e é registrado no EPIC-003) | Média | Médio | **MITIGADO POR DESIGN (proteger o clímax, §9.4):** o EPIC-004 adiciona **só ~45–90 min** de aula (grants + demo-dinheiro do bypass + cerimônia do Diploma). Todo o scaffolding de rede é **pré-provisionado pelo instrutor OFF-clock** (browser-harness). Sem provisionar VNet/PE/KV ao vivo. |
| 2 | **CAE imutável impõe ordem dura** (VNet+CAE primeiro, FQDN antes de `VITE_*`/CORS) — errar custa **rebuild do frontend** | Média | Médio | ADE-009 Inv 3 + aprendizado das Quartas (recriar a VNet do env mudou o FQDN). Sequenciamento explícito no **runbook browser-harness (4.5)** + pré-provisionamento pelo instrutor. |
| 3 | **MI+AAD no SQL tem cerimônia** (contained users, `Authentication=Active Directory Managed Identity`, propagação de RBAC que leva minutos) | Média | Médio | DDL **delegada a @data-engineer** (idempotente, ADE-000 Inv 2); instrutor pré-provisiona; grants são o hands-on curto do aluno. |
| 4 | **`X-Gateway-Key` é segredo simétrico compartilhado** (não é MI) — se vazar, é chave de origem | Baixa | Médio | Aceito como **irredutível** → vive no **KV** (Inv 2). Blast radius contido: por si só não dá acesso a dados (ainda exige alcançar a rede); rotação simples. |
| 5 | **Cache-pós-auth (4.4) mexe no pipeline do gateway** (perímetro ADE-004) e pode introduzir regressão | Média | Médio | **@architect consult obrigatório** no reorder; smoke de "cache HIT só com auth"; a mudança é de **ordem** (cache passo 3→pós-auth), não de mecanismo. |
| 6 | **PE custa e é efêmero (T2)** ~US$7/mês | Baixa | Baixo | **Showcase** — sobe para a aula, **derruba no mesmo dia** (Budget Alert + teardown). T1 (o núcleo) é US$0. |
| 7 | **Backend Node com 0 testes** (4.3 é o primeiro) — surface de teste imatura pode atrasar | Média | Baixo | Escopo cirúrgico: 4.3 cobre **só** `gatewayTrust.js` + idempotência SQL + startup guards (não "testar tudo"); frontend 0-testes segue **sinalizado** (débito, não escopo). |
| 8 | **Dependência do EPIC-003 para o Capstone** (Diploma precisa do F6; doc-fix toca o chatbot) | Média | Médio | 4.5/4.6 são as **últimas** stories; núcleo Blindar (4.1–4.4) roda **em paralelo** ao EPIC-003. @pm sincroniza o calendário dos dois épicos-irmãos. |
| 9 | **`gemini-2.5-flash` desliga em 2026-10-16** (herdado — afeta a narrativa do chatbot no Diploma/materiais) | Média | Médio | **SINALIZADO** (é risco do EPIC-003 3.3); ensinar o modelo como variável (`VITE_GEMINI_MODEL`). Não é escopo de código do EPIC-004, mas o material do Diploma deve referenciar o pin correto. |
| 10 | **Cold start de Functions/SignalR** trava a demo ao vivo (bypass 401/200, Diploma) | Média | Baixo | Warmup 5min antes do bloco (herdado). |

## Compatibility Requirements

- [x] **ADE-004/007/000/003 preservadas** — o hardening é **aditivo, não um reset** (ADE-009 §"O que NÃO muda"). Gateway segue guardião único do JWT; identidade `oid→X-Entra-OID→entra_oid` intacta; idempotência/`correlationId` intocados; "URL nunca hardcoded" endurecido.
- [x] **Labs sem gateway (Oitavas/F1) seguem funcionando** — o gating por `GATEWAY_SHARED_SECRET` vazio preserva o comportamento `Anonymous` legado (retrocompatibilidade).
- [x] **Fluxo v1 (Node/Express + bcrypt+JWT) intocado** — v2 é paralelo.
- [x] **DDL dos contained users é aditiva/idempotente** (ADE-000 Inv 2) — nenhuma alteração destrutiva de schema; trocar connection string por MI não muda a lógica de INSERT.
- [x] **Frontend:** Diploma vivo é rota/página **nova** (não altera o fluxo existente); usa o stack React/Vite atual.
- [x] **Impacto zero no fluxo v1; medível no v2** (App Insights).

## Decisões fechadas

| # | Decisão | Fonte / Ref |
|---|---|---|
| 1 | **Estrutura = EPIC-004 próprio (Nível Produção) + Capstone DENTRO dele (stories 4.5/4.6), NÃO EPIC-005** | `[AUTO-DECISION]` @pm (§Decisão estrutural) — 5 razões; válvula de escape a @sm/@po |
| 2 | **Escopo de segurança = T1 (US$0) + showcase T2 no SQL; T3 nomeado, não construído** | **ADE-009 §T1/T2/T3** + plano §4/§9.2 |
| 3 | **`X-Gateway-Key` (não Function PE) fecha o bypass** — generaliza o `gatewayTrust.js` das Quartas às Functions/McpServer | **ADE-009 Inv 1 / Alt 1** + plano §9.2 |
| 4 | **Data-plane por MI+AAD (zero connection string); irredutíveis (LLM keys, `GATEWAY_SHARED_SECRET`) no KV** | **ADE-009 Inv 2 / Alt 2/3** |
| 5 | **Menor-privilégio por serviço; McpServer = `db_datareader`-only** (regra de ouro vira garantia de RBAC) | **ADE-009 Inv 2** + ADE-008 Inv 1 |
| 6 | **Remover `AllowAllAzureServices`; SQL por PE (T2) ou MI-AAD restrito (T1)** | **ADE-009 Inv 4 / Alt 4** |
| 7 | **CAE imutável → VNet+CAE nascem primeiro; FQDN do gateway antes de `VITE_*`/CORS** | **ADE-009 Inv 3** + aprendizado das Quartas |
| 8 | **Consolidação (cache-pós-auth, testes idempotência/`gatewayTrust`/startup guards, `UseForwardedHeaders`) é escopo do EPIC-004, NÃO invariante da ADE-009** | **ADE-009 §Consequences** (rota explícita) + plano §5 |
| 9 | **Entrega = Template Repository novo (não fork); variáveis 15→1 via `lab.config.json`; segredos reais no KV** | plano §6 |
| 10 | **Scaffolding de rede pré-provisionado pelo instrutor (browser-harness offline); aluno faz grants + demo do bypass** | **ADE-009 §Escopo consciente / Alt 5** + plano §9.4 |
| 11 | **Diploma vivo (telemetria real, infalsificável, assinado "Squad AIOX + você")** como elemento memorável ~US$0 | plano §1 |
| 12 | **Identidade base v1 ↔ CIAM (fence `CiamOnly`) é EPIC-003/3.5 sob ADE-007 v1.3 — NÃO deste épico** | plano §3; ADE-007 v1.3 (só referência) |
| 13 | **ADE-004/007/000/003 preservadas; ADE-002 Inv 4 (tag n8n) MOOT reafirmado** | **ADE-009 §"O que NÃO muda"** |

## Decisões pendentes (carry-forward para o draft das stories)

| Decisão | Quando resolver | Responsável |
|---|---|---|
| **Nomes exatos das Managed Identities** + mapa fino de RBAC por serviço | Draft/provisionamento de 4.1 | @devops (+ @architect valida) |
| **DDL concreta dos contained users** (`CREATE USER ... FROM EXTERNAL PROVIDER`, grants) — literais não inventados na ADE | Draft de 4.1 | **@data-engineer** (delegado ADE-009) |
| **Literal/rotação do `GATEWAY_SHARED_SECRET`** e disponibilidade de VNet-integration no plano do CAE da turma | Draft/provisionamento de 4.1/4.2 | @devops |
| **Custo exato do PE por região** ("a confirmar" na ADE-009) | Provisionamento de 4.2 (T2) | @devops |
| **Fundir 4.3+4.4** (Consolidação única) OU manter separadas; **dividir 4.1/4.2** | Draft | @sm |
| **Onde o Diploma vivo é servido** (rota no frontend do lab vs página estática no Template Repo) e a fonte concreta de telemetria (App Insights query vs FlowEvents) | Draft de 4.6 | @sm/@dev (+ @ux design) |
| **Capstone in-épico vs EPIC-005** (se 4.5/4.6 incharem) | Draft | @po/@sm (+ owner) |
| **Encaixe de calendário** do EPIC-004 dentro da sessão unificada da Final | Pré-evento | @pm |

## Dimensionamento (honesto) — o que o EPIC-004 adiciona à aula vs ao build

> **Regra de ouro do dimensionamento (plano §9.4 — "proteger o clímax"):** o EPIC-004 é **pesado no build da squad** (~36–40h) e **pré-provisionamento do instrutor** (rede), mas **leve no relógio da aula**. O aluno **não** provisiona VNet/CAE/PE/KV ao vivo — ele faz **grants** (RBAC/contained users) + a **demo-dinheiro** do bypass, e recebe o **Diploma**. A rede é a **fundação**, não o espetáculo.

| Camada | O que é | Onde/quando | Custo de tempo |
|---|---|---|---|
| **Pré-provisionamento (instrutor)** | VNet + CAE VNet-integrado + SQL PE + KV + MIs semeados; segredos no KV (browser-harness offline) | **Fora do relógio da aula** | Instrutor, off-clock |
| **Ato Blindar (aluno, na aula)** | Grants (RBAC/contained users) + a **demo-dinheiro** (`curl` 401 direto / 200 via gateway) | Segmento da sessão da Final | **~45–75 min** |
| **Cerimônia do Diploma (aluno)** | Renderiza a telemetria real, assina "Squad AIOX + você", compartilha | Fecho da sessão da Final | **~15 min** |
| **Build da squad (off-class)** | 4.1–4.6 (código + testes + Template Repo + Diploma + diagrama) | Ciclo de entrega | **~36–40h** |

**Contribuição de aula do EPIC-004:** ~**45–90 min** dentro da sessão unificada da Grande Final (que já soma os blocos F5/F6 do EPIC-003 ~5,5–7,5h). **Risco de tempo (Risco #1):** registrado honestamente — é o mesmo que estourou nas Quartas. **Mitigação real:** o pré-provisionamento tira o maior sorvedouro (rede) do relógio; grants + demo são rápidos; o Diploma é celebração, não trabalho.

## Débitos sinalizados (não necessariamente resolvidos neste épico)

- **Frontend com ZERO testes automatizados** (React/Vite) — recorrente; o Diploma (4.6) adiciona UI nova. Candidato a AC mínima em 4.6 OU story transversal futura — decisão de @sm/@po (não escopo obrigatório).
- **CIAM e2e sem cobertura automatizada** — pertence ao EPIC-003/3.5 (identidade). Só referência aqui.
- **T3 aspiracional** (PE p/ KV+Service Bus, NAT, Function PE) — **nomeado, não construído** (roteiro de produção real).
- **`gemini-2.5-flash` shutdown 2026-10-16** — risco do EPIC-003 3.3; o material do Diploma deve citar o pin correto (`VITE_GEMINI_MODEL`).
- **CI/CD do fork/Template Repo incompleto** (CodeRabbit sem seat; secrets de turma) — 4.5 fecha o modelo de entrega; seats/secrets seguem operação de @devops (não-bloqueante).
- **Itens "a confirmar" da ADE-009** (nomes de MI, mapa fino de RBAC, custo exato do PE, VNet-integration no plano do CAE) — resolvidos no provisionamento/DDL (§Decisões pendentes).

## Stakeholders

- **Owner do evento:** Guilherme Prux Campos (guilherme.campos@tftec.com.br)
- **Audiência:** alunos da TFTEC (pós-graduação) + devs convidados (polyglot, background cloud)
- **Squad AIOX TFTEC:** @pm (Morgan) → @sm (River) → @po (Pax) → @dev (Dex) + **@data-engineer (Dara)** DDL contained users (delegado ADE-009) + **@architect (Aria)** gate ADE-009 + **@devops (Gage)** MI/RBAC/rede/KV + Template Repo/CI-CD + **@qa (Quinn)** gate da Consolidação + **@analyst (Atlas)** runbook/draw.io + **@ux-design-expert (Uma)** design do Diploma
- **Nota:** a **Semifinal é de outro instrutor (Raphael)** — fora do escopo deste épico.

## Próximos passos (PM → SM)

1. ✅ **ADE-009 Accepted** (gate liberado) — `docs/architecture/ade-009-network-secrets-service-identity.md`
2. ✅ **EPIC-004 criado** — este documento
3. ➡️ **@sm (River)** drafta as stories em `docs/stories/4.{1..6}.story.md` (confirma numeração 4.x com @po). **Ordem de draft recomendada:**
   - **1º — 4.1** (fundação de segredos: MI+KV; usa a §Validation da ADE-009 + a delegação de DDL a @data-engineer como esqueleto de AC).
   - **2º — 4.2** (o P0 / demo-dinheiro; ancora o `GATEWAY_SHARED_SECRET` no KV de 4.1; generaliza o `gatewayTrust.js`; remove o wildcard; SQL PE T2).
   - **3º — 4.3 e 4.4** (Consolidação, em paralelo; 4.4 abre com **@architect consult** no reorder do pipeline).
   - **4º — 4.5** (Entrega/Template Repo; precisa do núcleo Blindar pronto + das features do EPIC-003).
   - **5º — 4.6** (Diploma + draw.io; **último** — renderiza/documenta o estado final).
4. Cada story deve ter: **Status Draft** · Story do ponto de vista do **aluno** (Blindar hands-on, Diploma) ou do **squad** (wiring/testes/entrega) · **ACs rastreáveis** (Art. IV — cada AC cita **ADE-009 Inv X** / decisão do owner / plano §) · Tasks executáveis · Dev Notes com refs à §Verificação da ADE-009 · Validation (smoke: bypass 401/200, `grep` conn-string=0, cache-só-com-auth, dry-run do fluxo do aluno).
5. **@po (Pax)** valida cada story no checklist de 10 pontos antes de Ready; confirma a decisão **Capstone in-épico vs EPIC-005**.
6. **@architect (Aria)** é o quality gate de **4.1/4.2/4.5** (ADE-009 Inv 1–4) e consult de **4.4** (reorder do pipeline) e da fidelidade do **draw.io (4.6)**.
7. **@qa (Quinn)** é o quality gate da **Consolidação (4.3/4.4)**.
8. **@data-engineer (Dara)** entrega a **DDL dos contained users** (menor-privilégio, McpServer `db_datareader`-only) em 4.1 — **delegado pela ADE-009** (aditiva/idempotente).
9. **@devops (Gage)** provisiona MI/RBAC/rede/KV (4.1/4.2), pré-provisiona a topologia T2 (browser-harness offline), e constrói o **Template Repo + `lab-grande-final.yml`** (4.5) — CI/CD é autoridade @devops.
10. **@analyst (Atlas)** + **@ux (Uma)** entregam o runbook browser-harness (4.5), o **draw.io** e o design do **Diploma vivo** (4.6).

## Definition of Done (Epic-level)

- [ ] Stories 4.1–4.6 **Done** com QA gate **PASS**.
- [ ] **ADE-009 §Validation checklist (8 itens) 100% satisfeito:** bypass fechado (401 direto / 200 via gateway); `X-Gateway-Key` armado nas Functions/McpServer (timing-safe, fail-closed configurado / legado vazio); zero connection string no data-plane (`grep`=0); menor-privilégio confirmado (McpServer `db_datareader`-only; Entry=SB Sender, Consumer=Receiver); irredutíveis no KV via KV reference/MI; `AllowAllAzureServices` removido + `publicNetworkAccess` Disabled/restrito; ordem de rede respeitada (se T2); ADEs preservadas (ADE-004/007/000; ADE-002 Inv 4 MOOT).
- [ ] **Consolidação verde:** novos testes (idempotência SQL integração, `gatewayTrust.js`, startup guards) passando; **cache-pós-auth** (HIT só com auth); **`UseForwardedHeaders`** (rate-limit vê IP real); doc-fixes (`3`→`7` tools, typo `Ticker/Ticket`); 5-fixes-PRD como checklist no workflow.
- [ ] **Entrega sem-atrito:** **Template Repo `fifa2026-final-lab`** publicado ("Use this template" → repo próprio); **`lab.config.json`** (aluno edita 1 linha); **`lab-grande-final.yml`** (zero `az create`, `acao=check` Doctor, build+unit tests); **nenhum secret no GitHub** (só `AZURE_CREDENTIALS`; reais no KV); runbook browser-harness do instrutor entregue.
- [ ] **Diploma vivo** renderiza telemetria real (correlation-IDs, região, hora do deploy), infalsificável, assinado "Squad AIOX + você"; **diagrama draw.io** de topologia/segurança (ícones Azure reais) fiel à ADE-009.
- [ ] **Perímetro preservado + bases sólidas sem regressão** (ADE-004/007/000/003; fluxo v1 e Oitavas/Quartas intactos).
- [ ] **Cost report:** T1 ~US$0; PE (T2) ~US$7/mês derrubado no dia; ≤ US$15/aluno.
- [ ] **Grep de regressão de segurança:** senha/connection-string na config dos serviços = **0** ocorrências (tudo MI/KV).

---

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-07-01 | 0.1 | **EPIC-004 criado** — "Nível Produção" como **épico-irmão do EPIC-003** (co-entregam a mesma Grande Final: EPIC-003=features, EPIC-004=substrato), sob o gate **ADE-009** (rede fechada + secrets em cofre + identidade de serviço). **Decisão estrutural `[AUTO-DECISION]`:** a camada **Capstone & Entrega entra NO EPIC-004** (stories 4.5/4.6), **não** vira EPIC-005 — 5 razões (coerência temática "produção = segurança + entrega"; ausência de gate próprio ⇒ não é fronteira de épico, inverso da lógica que fundou o EPIC-003; amarração dura à ADE-009 Inv 2/3; precedente das transversais 2.7/3.6 in-épico; dimensionamento contido pelo pré-provisionamento §9.4) + válvula de escape a @sm/@po. **6 stories propostas:** 4.1 Blindar data-plane (MI+KV, contained users menor-privilégio) · 4.2 Blindar perímetro (`X-Gateway-Key` P0/demo-dinheiro + remover wildcard + SQL PE T2) · 4.3 Consolidação-testes (idempotência SQL + `gatewayTrust.js` + startup guards) · 4.4 Consolidação-fixes (cache-pós-auth + `UseForwardedHeaders` + doc-fixes + 5-fixes-PRD) · 4.5 Entrega (Template Repo + `lab.config.json` 15→1 + `lab-grande-final.yml` + runbook browser-harness) · 4.6 Capstone-experiência (Diploma vivo + draw.io). Escopo técnico = **as 4 invariantes da ADE-009 (4.1/4.2) + a Consolidação roteada por §Consequences (4.3/4.4) + a entrega/experiência que materializa Inv 2/3 (4.5/4.6)** — mapa invariante→story explícito (Art. IV). Dimensionamento honesto: **aula +~45–90 min** (grants + demo-dinheiro + Diploma), rede **pré-provisionada pelo instrutor off-clock** (§9.4 proteger o clímax); **squad build ~36–40h**. Delegações ADE-009 respeitadas (DDL contained users→@data-engineer; MI/RBAC/rede/KV→@devops). **Não re-escopa o EPIC-003** (identidade `CiamOnly`/ADE-007 v1.3 só referenciada, é 3.5). Preserva ADE-004/007/000/003; ADE-002 Inv 4 MOOT reafirmado. Numeração 4.x a confirmar por @sm/@po. Nada inventado além das fontes (Art. IV). | **@pm (Morgan) · Squad AIOX TFTEC** |

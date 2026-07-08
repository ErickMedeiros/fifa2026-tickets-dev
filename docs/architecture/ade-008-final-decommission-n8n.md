# ADE-008 — Descomissionar o n8n na Final: pós-compra inline na Function + chatbot só "sentidos" (Fase A) (supersede ADE-006)

> **Tipo:** Architecture Decision Entry (component removal + boundary re-scope — remove a camada de orquestração/ação externa)
> **Status:** ✅ Accepted · **SUPERSEDES ADE-006** · v1.0
> **Date:** 2026-06-30
> **Author:** Aria (Architect)
> **Scope:** EPIC-002 **"a Final"** (último lab do Living Lab — concentra F5 chatbot/MCP + F6 Flow Visualizer + o resto da orquestração pós-compra). Materializa-se em `src/Fifa2026.V2.Functions/` (pós-compra), `src/Fifa2026.V2.FlowEvents/` (re-nós do visualizer), `src/Fifa2026.V2.McpServer/` (chatbot só read-only), na infra (`infra/phase-04/`, `deploy-phase-04.yml`) e no roteiro de aula da Final.
> **Supersedes:** **ADE-006** (Chatbot agêntico: split sentidos/mãos + n8n como hub de orquestração + Fase B "dois agentes"). A **regra de ouro é preservada** (simplificada); o **split sentidos/mãos** sobrevive só nos **sentidos**; a camada de **ação/orquestração via n8n** (Inv 4/7, Fases B e C) é **removida**. Ver §"O que muda vs ADE-006".
> **Related:** ADE-000 (microsserviço paralelo — **Inv 4 idempotência** e **Inv 5 W3C Trace Context/correlationId** são o alicerce do novo pós-compra; ADE-000 Inv 5 recebe **correção de doc M-1**, ver Inv 3), ADE-002 (pinning — **Inv 4 "tag n8n" fica MOOT**; ver §Impacto), ADE-004 (gateway YARP guardião — **preservada**), ADE-007 (identidade CIAM dual-issuer — **preservada, inalterada**). ADE-005 permanece superseded por ADE-007.
> **Rastreabilidade (Art. IV):** as 3 decisões abaixo foram **tomadas pelo owner (Guilherme Prux Campos) em 2026-06-30**; esta ADE apenas as **formaliza** (não as re-litiga). Fontes: `.claude/agent-memory/aiox-architect/project_final-scope-n8n-removal.md` (recomendação da Aria, insumo primário), `.claude/agent-memory/aiox-po/project_final_epic_backlog.md` (backlog/débitos do @po), `.claude/agent-memory/aiox-data-engineer/project_n8n_removal_postgres.md` + `project_final_data_tasks.md` (impacto de dados — decomissiona o PostgreSQL), `docs/architecture/ade-006-chatbot-agentic-interface.md` (a ADE superseded). Toda afirmação sobre o código foi **verificada na fonte da verdade** (arquivos `.cs` reais — ver §Verificação na fonte da verdade); itens não confirmáveis estão marcados **"a confirmar"**.

---

## Context

O owner decidiu, em **2026-06-30**, **remover o n8n** do stack da **Final** (o próximo e último lab do Living Lab FIFA 2026 Tickets, ainda não draftado). O enquadramento inicial foi "sai a orquestração pós-compra via webhook".

**Honestidade arquitetural (Art. IV) — o alcance é maior do que "só o pós-compra".** Verificado no código real (`project_final-scope-n8n-removal.md`), o n8n aparece em **três** lugares do EPIC-002, e a remoção atinge os três:

1. **Pós-compra (F4):** a `PurchaseConsumerFunction` dispara um **webhook n8n** (`N8nWebhookNotifier` → `N8N_WEBHOOK_URL`) após gravar a compra. É o que o owner citou.
2. **F6 Flow Visualizer:** o webhook n8n é um **nó da jornada de compra** (`FlowEventType.N8N_WEBHOOK_TRIGGERED`) — a "bolinha" animada atravessa esse nó.
3. **F5 chatbot agêntico (ADE-006 Fase B/C):** o n8n é o **"Agente 2 / o COMO"** — a "mão" `criar_alerta_ingresso` dispara um **webhook n8n** (`AlertWebhookNotifier` → `N8N_ALERT_WEBHOOK_URL`) que, no design da ADE-006 v1.1, alimentaria um **AI Agent node** dentro do n8n. A **Fase A** (7 sentidos read-only, Story 2.8 **Done**) **não depende de n8n** e sobrevive intacta.

A ADE-006 é declarada **imutável durante o EPIC-002 — mudança exige nova ADE que a supersede**. Remover o n8n é exatamente essa mudança. Esta ADE-008 é o **GATE** do épico da Final: `@pm` e `@sm` **não** draftam épico/stories antes dela existir.

### As 3 decisões do owner (2026-06-30) — LEI, não re-litigadas aqui

| # | Decisão do owner | Efeito arquitetural |
|---|---|---|
| **1** | **Pós-compra = Function inline + FlowEvent.** A `PurchaseConsumerFunction` faz a notificação **inline** (mock HTTP/log) e emite o rastro correlacionado (`correlationId`), em vez de chamar o webhook do n8n. | **Decomissiona** o Container App do n8n **e** o **PostgreSQL gerenciado** que ele exigia. (Logic Apps **rejeitado**: troca um orquestrador por outro, não realiza a simplificação.) |
| **2** | **Chatbot = só Fase A.** Mantém os 7 sentidos read-only (Story 2.8 Done). **Dropa** a "mão" `criar_alerta_ingresso` (Fase B / Story 2.9), o "Agente 2 / AI Agent node do n8n" e a **Fase C**. | A **regra de ouro se mantém** ("o chatbot nunca escreve direto no banco") — agora **trivialmente**, pois não há mais camada de ação. |
| **3** | **Formato = aluno constrói tudo hands-on** (F5 chatbot/MCP + F6 Flow Visualizer). | Orienta o formato das stories (o `@sm` drafta labs hands-on, não demonstrações guiadas). |

---

## Decision

Adotamos o pattern **"Final sem orquestração externa: pós-compra inline na Function + chatbot read-only (só sentidos)"**, com **6 invariantes**. A peça central é a **preservação da regra de ouro** por **eliminação da superfície de ação** (Invariante 1), e a **substituição do webhook n8n por notificação inline** que preserva ADE-000 Inv 4/Inv 5 (Invariante 3).

### Invariante 1 — REGRA DE OURO preservada e agora TRIVIAL: o chatbot nunca escreve direto no banco

A regra de ouro da ADE-006 (Inv 1) **permanece em vigor**: o LLM (no front) e o McpServer **jamais** executam INSERT/UPDATE/DELETE disparados por uma conversa. A diferença é que, sem a camada de ação (n8n / "mãos"), a regra passa a valer **por construção, não por roteamento**:

- **Antes (ADE-006):** a regra se sustentava roteando ações por caminhos idempotentes/observáveis (Service Bus para compra; **webhook n8n** para orquestração). Havia uma superfície de ação (`ReadOnly=false`) cujo blast radius precisava ser contido.
- **Agora (ADE-008):** **não há superfície de ação no chatbot.** O McpServer expõe **somente sentidos read-only**. A regra de ouro é uma propriedade estrutural verificável por inspeção: `FifaQueryRepository` só faz `SELECT` (verificado — zero `INSERT`/`UPDATE`/`DELETE`/`ExecuteAsync`; cabeçalho "Acesso SOMENTE leitura — o McpServer nunca grava").

> **Por quê continua sendo a regra de ouro:** um LLM alucina argumentos. A ADE-006 continha o dano roteando ação por n8n; a ADE-008 **elimina o vetor** — não há tool que aja. É a forma mais forte da regra: o pior caso de uma alucinação no chat é uma leitura errada, nunca uma escrita.

### Invariante 2 — O chatbot da Final é SÓ "sentidos" (Fase A, read-only); a natureza "mãos" sai de escopo

O split **"sentidos vs mãos"** da ADE-006 (Inv 2) sobrevive **apenas na metade "sentidos"**:

- **Sentidos (ler)** — `[McpServerTool(ReadOnly = true)]` → `IFifaQueryRepository` → **SQL SELECT parametrizado** (Dapper). **Preservados na íntegra**: as 7 tools da Fase A (as 3 originais + as 4 da Story 2.8), verificadas e **Done**.
- **Mãos (agir)** — `ReadOnly=false` → webhook n8n. **Fora de escopo da Final.** A única "mão" existente (`criar_alerta_ingresso`, `FifaActionTools.cs`) e seu notifier (`AlertWebhookNotifier`) **não são evoluídos** e passam a **legado a aposentar** (§Impacto).

O McpServer permanece com **ingress interno, atrás do gateway YARP** (ADE-004), read-only, sem revalidar JWT. A superfície de tools do chat volta a ser **homogênea (só leitura)** — o discriminador `ReadOnly` continua existindo no SDK, mas na prática toda tool em escopo é `ReadOnly=true`.

### Invariante 3 — Pós-compra é INLINE na `PurchaseConsumerFunction`: notifica (mock) + emite rastro correlacionado; sem n8n

O caminho pós-compra deixa de sair para o n8n e passa a ser **inline no consumer**, preservando **ADE-000 Inv 4 (idempotência)** e **ADE-000 Inv 5 (W3C Trace Context / `correlationId`)**:

- **Onde:** dentro do bloco `case InsertOutcome.Inserted` da `PurchaseConsumerFunction.RunAsync` — **exatamente onde hoje está a chamada `_n8nNotifier.NotifyPurchaseAsync(...)`** (verificado: `PurchaseConsumerFunction.cs`, linhas 83-102). A notificação dispara **apenas em `Inserted`**, **nunca em `Duplicate`** (idempotência: uma reentrega do Service Bus não re-notifica) — comportamento que **já existe** e é preservado.
- **O quê:** a notificação inline é um **mock** (HTTP POST a um endpoint inócuo **OU** apenas um log estruturado — **forma concreta a confirmar** no draft da story; ambas honram a decisão do owner "mock HTTP/log"). A dependência `IN8nWebhookNotifier` é **removida** do construtor; nenhuma URL de n8n é lida.
- **Rastreabilidade (o "FlowEvent"):** o consumer continua emitindo o **rastro correlacionado** via `_logger.BeginScope(new { CorrelationId })` (verificado: `PurchaseConsumerFunction.cs`, linha 62) — o mecanismo que alimenta o F6. A notificação inline emite **seu próprio trace correlacionado** (ex.: "Notificação pós-compra enviada (correlationId=…)"). **Nada é perdido na cadeia de rastreabilidade.**

> **Honestidade arquitetural (Art. IV) — o que "emitir o FlowEvent" significa de fato.** No F6 real (verificado), **os componentes NÃO empurram objetos `FlowEvent`**. Eles emitem **traces com `customDimensions.CorrelationId`** (via `ILogger.BeginScope`); o serviço `Fifa2026.V2.FlowEvents` **consulta** o Log Analytics por `correlationId` (`AppInsightsFlowEventRepository` → Kusto `traces | where customDimensions.CorrelationId == …`) e **classifica** cada trace num nó tipado (`TraceEventMapper.Classify(role, message)`), empurrando o `FlowEvent` resultante via SignalR (`FlowHub`). Portanto, "a Function emite o FlowEvent" = **a Function emite o trace correlacionado que o motor do F6 mapeia** — não um push direto ao SignalR. Essa distinção é o que torna a Invariante 5 verdadeira: o motor não sabe (nem se importa) se o trace veio de um webhook n8n ou de uma notificação inline.

> **Correção de doc herdada (M-1 do gate S2.6) — dobrada aqui.** O gate `docs/qa/2026-06-06-architect-gate-S2.6.md` deixou pendente um **doc-fix**: **AC-4 hop 2** e **ADE-000 Inv 5** dizem que o `correlationId` viaja em `ApplicationProperties["CorrelationId"]` (Service Bus), mas a **implementação real usa o CORPO da mensagem + `BeginScope`** — verificado no comentário canônico de `PurchaseConsumerFunction.cs` (linhas 79-82: *"O payload sai do CORPO da mensagem (correlationId/entraOid), não das Application Properties do Service Bus"*) e no `BeginScope` da linha 62. **O código está certo; o doc atrasou.** Esta ADE **registra a correção**: onde ADE-000 Inv 5 e a AC-4 falarem `ApplicationProperties`, a fonte da verdade é **corpo da mensagem + `ILogger.BeginScope` → `customDimensions.CorrelationId`**. A edição textual de ADE-000/AC-4 é um **registro** (não implemento; `@po`/`@sm` aplicam no doc-fix).

### Invariante 4 — O n8n é REMOVIDO como componente e como camada de orquestração/ação

O n8n deixa de existir no stack da Final. Concretamente:

- **Componente:** o **Container App do n8n** e o **PostgreSQL gerenciado** (persistência **exclusiva** do n8n — verificado por `@data-engineer`: zero `Npgsql` no repo; todo o data tier de app é Azure SQL) são **decomissionados**. Detalhe de dados (dump de cortesia + drop) **delegado a `@data-engineer`** (`project_n8n_removal_postgres.md`).
- **Camada de ação/orquestração:** a **Invariante 4 da ADE-006** ("Mãos = webhook n8n, e dentro do n8n o cérebro é o AI Agent node") é **REMOVIDA**. Não há mais "Agente 2", AI Agent node, MCP Client Tool n8n→McpServer, nem "dois agentes cooperando".
- **F4 (Story 2.4):** a fase inteira era orquestração n8n (`post-purchase-notification` workflow). Com a remoção, **F4 é aposentada** do lab; sua função (notificar pós-compra) é absorvida **inline** pela Invariante 3.

### Invariante 5 — F6 Flow Visualizer passa de 6 para 5 nós; o MOTOR é inalterado

A jornada de compra do F6 **perde o nó do n8n**. Hoje o `FlowEventType` (verificado) tem **6 membros** (ordinais 0-5):

```
0 GATEWAY_YARP_RECEIVED → 1 FUNCTION_ENTRY_PROCESSED → 2 SERVICE_BUS_PUBLISHED
→ 3 FUNCTION_CONSUMER_DONE → 4 N8N_WEBHOOK_TRIGGERED → 5 SQL_INSERTED
```

O n8n é o **`N8N_WEBHOOK_TRIGGERED = 4`** — o **5º nó em contagem humana (1-based)**; é o "nó 5" citado nas memórias. Removido, a jornada passa a **5 nós**:

```
Gateway YARP → Entry → Service Bus → Consumer → SQL
```

com a **notificação inline dentro do nó Consumer** (`FUNCTION_CONSUMER_DONE`), não como nó próprio.

**O motor não muda.** Verificado: o F6 é (a) `AppInsightsFlowEventRepository` (Kusto por `correlationId`), (b) `TraceEventMapper.Classify` (trace → nó), (c) `FlowHub`/`SignalRFlowEventPublisher` (push por grupo `correlation-<id>`), (d) `FlowEndpoints` (`/api/flow/recent`, `/{id}`, `/{id}/replay`). **Nenhum desses depende do n8n** — só a **lista de nós** o referencia. A mudança é cirúrgica (implementação é do `@dev`, apontada aqui):

- Remover o membro `N8N_WEBHOOK_TRIGGERED` do enum `FlowEventType` e **renumerar `SQL_INSERTED` de 5 → 4** (o `FlowEvent.NodeIndex` deriva do ordinal — a animação da bolinha usa o índice).
- Remover o branch de classificação n8n em `TraceEventMapper.Classify` (o `if (msg.Contains("webhook n8n") …)`, hoje linhas 56-60).
- Ajustar o `FlowDiagram` do front (6 → 5 nós) e a **AC-7** do smoke ("bolinha atravessa **5** nós <30s", não 6).

> **Trade-off honesto:** o visualizer deixa de exibir um hop explícito de "orquestração/notificação" — a notificação pós-compra passa a ser **invisível na animação** (dobrada no nó Consumer). É uma pequena perda didática, aceita em nome da simplificação. **Opção deferida (a confirmar no draft da story, não re-litigada aqui):** se o owner quiser manter a notificação **visível**, o `@sm`/`@dev` pode preservar um 6º nó `NOTIFICATION_SENT` (renomear o slot do n8n) alimentado pelo trace da notificação inline — mas o **default desta decisão é 5 nós**.

### Invariante 6 — Perímetro e ADEs preservadas: ADE-004, ADE-007, ADE-000 e ADE-002 (exceto o pin do n8n) permanecem

Declaração explícita do que **NÃO muda** (Art. IV — a supersessão é cirúrgica, não um reset):

- **ADE-004 (gateway YARP guardião):** **inalterada.** Todo tráfego do chat (só sentidos, agora) e da compra entra pelo gateway, que valida o JWT, injeta `X-Correlation-ID` e propaga `X-Entra-OID`. O McpServer segue com ingress interno atrás do gateway.
- **ADE-007 (identidade CIAM dual-issuer):** **inalterada.** Cliente no External ID (CIAM), admin no workforce; o encadeamento `oid → X-Entra-OID → entra_oid` não é tocado. (A remoção do n8n **não** interfere na identidade.)
- **ADE-000 (invariantes foundational):** **preservada** — Inv 4 (idempotência) e Inv 5 (correlationId) são o **alicerce** do novo pós-compra inline (Inv 3). Única alteração: o **doc-fix M-1** do texto da Inv 5 (Service Bus hop = corpo+BeginScope, não `ApplicationProperties`) — registro, não mudança de decisão.
- **ADE-002 (pinning):** preservada **exceto a Inv 4 ("tag n8n")**, que fica **MOOT** — sem n8n, não há tag a pinar. O pin dos pacotes/SDK .NET (Inv 1/5) segue em vigor. Ver §Impacto (registro, não edito ADE-002 nesta task).
- **ADE-006 Inv 7 (leste-oeste n8n → McpServer):** **MOOT** — não há mais n8n chamando o McpServer, logo não há tráfego leste-oeste a justificar. A anti-alucinação Inv 7 da ADE-006 ("verificar nós n8n vivos antes de afirmar") também fica **MOOT** (não há instância n8n a verificar).

---

## O que muda vs ADE-006 (esta ADE a SUPERSEDE)

| Elemento da ADE-006 (superseded) | ADE-008 (esta) |
|---|---|
| **Inv 1 — regra de ouro** (ação roteada por Service Bus **OU** webhook n8n) | **Preservada e SIMPLIFICADA** — não há camada de ação; McpServer read-only é a única superfície. A regra vale por construção. |
| **Inv 2 — split sentidos/mãos** | **Só "sentidos" sobrevivem.** "Mãos" (`ReadOnly=false`) saem de escopo da Final. |
| **Inv 3 — sentidos = SQL SELECT parametrizado** | **Inalterada** — Fase A / Story 2.8 (Done), 7 tools read-only. |
| **Inv 4 — mãos = webhook n8n + AI Agent node (dois agentes)** | **REMOVIDA** — sem n8n, sem AI Agent, sem "Agente 2", sem "dois agentes cooperando". |
| **Inv 5 — poucas tools bem parametrizadas** | **Inalterada** (princípio de design permanece válido para os sentidos). |
| **Inv 6 — tudo pelo gateway; identidade `oid`** | **Inalterada** (ADE-004/ADE-007 preservadas). |
| **Inv 7 — n8n → McpServer é leste-oeste (bypass do gateway)** | **MOOT** — não há mais n8n chamando o McpServer. |
| **Anti-alucinação Inv 7 — "verificar nós n8n vivos"** | **MOOT** — não há instância n8n a verificar. |
| **Fase A (7 sentidos)** | **Preservada intacta** (Story 2.8 Done). |
| **Fase B (`criar_alerta_ingresso` + AI Agent n8n)** | **DROPADA** — fora de escopo da Final. Código vira legado a aposentar. |
| **Fase C (n8n externo + FlowEvent "dois agentes")** | **DROPADA.** A jornada de compra do F6 é re-desenhada **sem n8n** (5 nós). A "jornada de chat de dois agentes" não é entregue. |
| **Pós-compra (F4) = webhook n8n** | **Substituído por notificação inline** na `PurchaseConsumerFunction` (Inv 3). |

**O que NÃO muda (preservado):** a regra de ouro (reforçada), os 7 sentidos read-only, o gateway YARP como guardião (ADE-004), a identidade CIAM (ADE-007), as invariantes foundational (ADE-000), o motor do F6, e o pin .NET da ADE-002.

---

## Rationale

### Por que notificação inline (vs manter um orquestrador)?

- **Realiza a simplificação que o owner quer.** Remover o n8n só vale a pena se a etapa que ele fazia (notificar pós-compra) for absorvida **sem** reintroduzir um segundo orquestrador. A notificação é um efeito **fire-and-forget best-effort** — não precisa de motor de workflow; um POST/log inline dentro do consumer basta.
- **Reduz superfície operacional.** Sai um **Container App** + um **PostgreSQL gerenciado** (persistência exclusiva do n8n, que já se provou frágil em sqlite+Azure Files e virou workaround Postgres). Menos custo, menos peças, menos "runtime que nunca fechou em PRD" (o workflow n8n da Fase B nunca foi montado ao vivo).
- **Preserva o que importa.** ADE-000 Inv 4 (idempotência: notifica só em `Inserted`) e Inv 5 (`correlationId`) continuam válidas — a rastreabilidade do F6 é preservada porque depende de **traces correlacionados**, não do n8n.

### Por que o chatbot fica só com "sentidos" (vs re-hospedar a ação)?

- **A "mão" existia para provar o padrão agêntico via n8n** (ADE-006 Fase B). Sem n8n, o valor pedagógico dessa prova some. O owner optou por **entregar a experiência de exploração (7 sentidos, já Done)** e **não** reconstruir a camada de ação.
- **Fortalece a regra de ouro.** Um chatbot só-leitura é a demonstração mais limpa de "IA que consulta mas não corrompe" — sem precisar explicar roteamento por Service Bus/webhook para conter alucinação.

### Por que o risco técnico é baixo?

- O pós-compra inline **reusa o ponto de extensão que já existe** (o `case Inserted` do consumer) — é **trocar uma chamada por outra**, não reescrever fluxo.
- O F6 é **trace-driven**: remover um nó é apagar um membro de enum + um branch de classificação. O motor (Kusto + SignalR) é agnóstico à lista de nós.
- Os dois webhooks n8n já são **fail-open/no-op quando a URL está vazia** (verificado nos dois notifiers) — remover o n8n **não quebra** compra nem chat mesmo antes da limpeza do código; só some o sink.

---

## Consequences

### Positivas

- ✅ **Regra de ouro na sua forma mais forte:** chatbot read-only, zero vetor de escrita a partir do LLM.
- ✅ **Menos superfície operacional:** sai o Container App do n8n + o PostgreSQL gerenciado (custo/atrito/persistência frágil eliminados).
- ✅ **Pós-compra mais simples e igualmente rastreável:** notificação inline preserva ADE-000 Inv 4/Inv 5; o F6 continua funcionando por traces correlacionados.
- ✅ **Fecha débitos abertos:** a Fase B "runtime nunca fechado em PRD" deixa de ser dívida (vira legado a remover); o pin `latest` do n8n (ADE-002 Inv 4, sem reprodutibilidade entre turmas) some.
- ✅ **F6 mais enxuto (5 nós):** narrativa de compra direta (Gateway → Entry → Service Bus → Consumer → SQL) sem um hop de orquestração externa que confundia.
- ✅ **Formato hands-on preservado (decisão 3):** aluno constrói F5 (chatbot só-sentidos) + F6 (visualizer 5 nós) — escopo mais fechado e ensinável em uma aula.

### Negativas / Trade-offs aceitos

- ⚠️ **Perde-se a narrativa "dois agentes cooperando"** (ADE-006 Fase B/C) — o ouro pedagógico de "o chat decide O QUÊ, o agente n8n decide O COMO". Aceito: o owner priorizou a simplificação; a Final entrega exploração read-only, não atuação agêntica.
- ⚠️ **A notificação pós-compra fica invisível no visualizer** (dobrada no nó Consumer). Mitigado: opção deferida de manter um nó `NOTIFICATION_SENT` se o owner quiser (Inv 5); default é 5 nós.
- ⚠️ **Código legado a aposentar** (`N8nWebhookNotifier`, `AlertWebhookNotifier`, tool `criar_alerta_ingresso`, `infra/phase-04/`, `deploy-phase-04.yml`, App Settings `N8N_*`) e **testes** associados (`N8nWebhookNotifierTests`, partes de `FifaActionToolsTests`/`PurchaseConsumerFunctionTests`). É trabalho de `@dev`/`@devops` (apontado, não executado aqui). Enquanto não removido, é código morto tolerado (fail-open).
- ⚠️ **A "mão" `criar_alerta_ingresso` some sem substituto** — se um dia a Final quiser "alertas", precisará de **nova decisão** (tabela `alerts` em Azure SQL + ação idempotente, o que **re-abre a regra de ouro** e exige ADE) OU outro sink. Fora de escopo desta ADE (registrado como débito).
- ⚠️ **`@data-engineer` precisa decomissionar o Postgres com cuidado** (dump de cortesia antes do drop, mesmo sendo dados efêmeros do n8n).

---

## Alternatives Considered (rejeitadas)

### Alt 1 — Substituir o n8n por Azure Logic Apps (outro orquestrador gerenciado)

- **Rejeitada porque:** só **troca um orquestrador por outro**. Não realiza a simplificação que o owner quer (continua havendo um serviço de workflow externo, com sua própria cerimônia de configuração e um "AI Agent"/conector para carregar a Fase B). A notificação pós-compra é fire-and-forget trivial — não justifica um motor de workflow. Explicitamente descartada pelo owner (2026-06-30).

### Alt 2 — Re-hospedar o "Agente 2" numa Function + Gemini (ação via Service Bus → NotificationFunction → Gemini)

- **Rejeitada porque:** preservaria a pedagogia "dois agentes" **sem** n8n, mas ao custo de **construir uma nova Function inteligente** (LLM server-side, novo fluxo Service Bus, nova observabilidade) — o oposto da simplificação. O owner decidiu que a Final entrega **só a Fase A** (read-only). Fica registrada como o caminho a considerar **se** um dia a pedagogia "dois agentes" voltar a ser must-have — mas não é o escopo da Final.

### Alt 3 — Manter o n8n

- **Rejeitada pelo owner** (2026-06-30): o custo/atrito operacional (Container App + PostgreSQL gerenciado) e o **runtime manual que nunca fechou em PRD** (o workflow `chat-alert-ingresso` da Fase B nunca foi montado ao vivo; a validação e2e do n8n exigia Postgres porque sqlite+Azure Files se mostrou inviável) não se pagam no contexto do lab.

### Alt 4 — Manter `criar_alerta_ingresso` fazendo a ação gravar numa tabela `alerts` no Azure SQL (novo sink, sem n8n)

- **Rejeitada porque:** faria a "mão" **escrever no banco a partir do LLM** — **quebra a regra de ouro** (ADE-006 Inv 1, preservada aqui na Inv 1). Reintroduziria a superfície de ação que o owner justamente tirou, e exigiria re-decisão explícita (RLS/ownership por usuário, idempotência). A Final é **só sentidos**; alertas ficam como débito futuro com ADE própria se ressuscitados.

---

## Validation

Esta decisão é considerada **validada** quando (checklist para o `@dev`/`@sm`/`@devops` na Final — não implemento; aponto):

- [ ] `PurchaseConsumerFunction` **não** referencia mais `IN8nWebhookNotifier`; a notificação pós-compra é **inline** (mock HTTP/log) dentro do `case InsertOutcome.Inserted` (nunca em `Duplicate` — ADE-000 Inv 4).
- [ ] A notificação inline emite um **trace correlacionado** (`ILogger.BeginScope({CorrelationId})`); o `correlationId` continua vindo do **corpo** da mensagem, **não** de `ApplicationProperties` (M-1; ADE-000 Inv 5 corrigida no doc).
- [ ] **n8n decomissionado:** Container App do n8n + **PostgreSQL gerenciado** removidos; App Settings `N8N_WEBHOOK_URL` e `N8N_ALERT_WEBHOOK_URL` removidos dos serviços.
- [ ] **F6 = 5 nós:** `FlowEventType` sem `N8N_WEBHOOK_TRIGGERED`; `SQL_INSERTED` renumerado 5→4; branch n8n removido de `TraceEventMapper.Classify`; `FlowDiagram` do front com 5 nós; AC-7 smoke = "bolinha atravessa 5 nós".
- [ ] **McpServer permanece read-only** (`FifaQueryRepository` só `SELECT`); os 7 sentidos da Fase A (Story 2.8) intactos; nenhuma tool `ReadOnly=false` em escopo.
- [ ] **Legado aposentado:** `N8nWebhookNotifier`/`IN8nWebhookNotifier`/`N8nWebhookPayload` (Functions), `AlertWebhookNotifier`/`IAlertWebhookNotifier`/`AlertPayloads` + tool `criar_alerta_ingresso` (McpServer), `infra/phase-04/`, `deploy-phase-04.yml` e testes associados — removidos ou marcados legado.
- [ ] **ADE-002 Inv 4 (tag n8n)** anotada como **MOOT** (registro; n8n não existe mais). Pins .NET intactos.
- [ ] **ADE-004/ADE-007/ADE-000 inalteradas** (exceto o doc-fix M-1 de ADE-000 Inv 5); McpServer atrás do gateway.

---

## Impact on EPIC-002 (a Final)

> **NÃO edito código nem stories** (autoridade de `@dev`/`@sm`/`@po`). Esta ADE **aponta** o impacto e é o **GATE** do épico. Detalhe de dados (decomissionar Postgres) delegado a **`@data-engineer`**.

| Story / Item | Impacto | Ação (executor) |
|---|---|---|
| **2.8 (Fase A / F5+ sentidos)** | ✅ **Preservada intacta** (Done). Os 7 sentidos read-only são a base do chatbot da Final. | Nenhuma — referência. |
| **2.9 (Fase B)** | **Fora de escopo da Final.** A "mão" `criar_alerta_ingresso` + `AlertWebhookNotifier` + `N8N_ALERT_WEBHOOK_URL` viram **legado a aposentar**. A Story fica marcada Done no gate, mas seu runtime (workflow n8n) nunca fechou — não será retomado. | `@sm` **não** drafta continuação; `@dev` aposenta o código na limpeza. |
| **Fase C (órfã, sem nº de story)** | **Dropada.** A jornada agêntica "dois agentes" com n8n não é entregue. O slot "2.10" **não** era a Fase C (foi reusado para compra multi-item — Story 2.10 Done). | `@pm`/`@sm` — não criam story de Fase C com n8n. |
| **2.4 (F4 / orquestração n8n)** | **Aposentada** do lab. A notificação pós-compra é absorvida **inline** (Inv 3). `N8nWebhookNotifier`/`N8N_WEBHOOK_URL`/`infra/phase-04/`/`deploy-phase-04.yml` = legado. | `@dev` implementa a Inv 3; `@data-engineer` decomissiona o Postgres. |
| **2.6 (F6 Flow Visualizer)** | **Re-nós: 6 → 5.** Remove `N8N_WEBHOOK_TRIGGERED` + branch do `TraceEventMapper`; renumera `SQL_INSERTED`; ajusta `FlowDiagram` e AC-7. **Motor inalterado.** Aplica o **doc-fix M-1** (ADE-000 Inv 5 / AC-4: corpo+BeginScope, não `ApplicationProperties`). | `@sm` drafta a story de re-nós; `@dev` implementa; `@po` aplica o doc-fix M-1. |
| **JIT provisioning CIAM** (débito #4 do `@po`) | **Fora do escopo desta ADE** (é identidade — governado por ADE-007). Apenas sinalizado como débito paralelo da Final. | `@pm`/`@sm` avaliam story própria (não depende desta ADE). |
| **Limpeza de código/infra n8n** | Legado a remover: `N8nWebhookNotifier`+`IN8nWebhookNotifier`+`N8nWebhookPayload`, `AlertWebhookNotifier`+`IAlertWebhookNotifier`+`AlertPayloads`+`FifaActionTools.criar_alerta_ingresso`, App Settings `N8N_WEBHOOK_URL`/`N8N_ALERT_WEBHOOK_URL`, `infra/phase-04/`, `.github/workflows/deploy-phase-04.yml`, e testes (`N8nWebhookNotifierTests` etc.). | `@dev` (código/testes) + `@devops` (workflow/infra). |
| **ADE-002 Inv 4 (tag n8n)** | Fica **MOOT** — anotar no doc de ADE-002 (registro). Pins .NET seguem. | `@architect` (registro em sessão de doc-fix, não nesta task). |

---

## Verificação na fonte da verdade (Art. IV)

Confirmado no código real (não afirmado de memória):

| Afirmação | Verificado em |
|---|---|
| Pós-compra dispara webhook n8n só em `Inserted`; payload sai do **corpo**; `BeginScope` propaga correlationId | `src/Fifa2026.V2.Functions/Functions/PurchaseConsumerFunction.cs` (l.62, 83-102, comentário l.79-82) |
| `N8nWebhookNotifier` = POST fire-and-forget a `N8N_WEBHOOK_URL`, no-op se vazio | `src/Fifa2026.V2.Functions/Data/N8nWebhookNotifier.cs` |
| `criar_alerta_ingresso` é `ReadOnly=false` (default omitido), dispara `AlertWebhookNotifier`, zero SQL, degradação graciosa | `src/Fifa2026.V2.McpServer/Tools/FifaActionTools.cs` |
| `AlertWebhookNotifier` = POST a `N8N_ALERT_WEBHOOK_URL`, no-op se vazio | `src/Fifa2026.V2.McpServer/Data/AlertWebhookNotifier.cs` |
| McpServer read-only (só `SELECT`; zero `INSERT`/`UPDATE`/`DELETE`); cabeçalho "o McpServer nunca grava" | `src/Fifa2026.V2.McpServer/Data/FifaQueryRepository.cs` |
| F6 é trace-driven: Kusto por `customDimensions.CorrelationId` → `TraceEventMapper.Classify` → `FlowHub` SignalR | `src/Fifa2026.V2.FlowEvents/Data/AppInsightsFlowEventRepository.cs`, `Data/TraceEventMapper.cs`, `Hubs/FlowHub.cs`, `Hubs/SignalRFlowEventPublisher.cs`, `FlowEndpoints.cs` |
| n8n é o nó `N8N_WEBHOOK_TRIGGERED = 4` (5º nó, 1-based); jornada tem 6 nós hoje | `src/Fifa2026.V2.FlowEvents/Models/FlowEventType.cs`, `Models/FlowEvent.cs` |
| Story 2.8 = Done; Story 2.9 = Done no gate (runtime n8n nunca fechou); Story 2.10 = Done = **compra multi-item** (não Fase C) | `docs/stories/2.8.story.md`, `2.9.story.md` (Change Log Task 6 pendente), `2.10.story.md` (AC-6) |
| ADE-002 Inv 4: tag n8n atual = `n8nio/n8n:latest` (owner override 2026-06-06); `2.23.4` foi o pin original/alternativa | `docs/architecture/ade-002-mcp-pinning.md` (Inv 4, Alt 1b, Change Log) |

**A confirmar (não inventado):**
- Forma concreta da notificação inline (HTTP mock vs só log) — detalhe de implementação do draft da story.
- Manter ou não um 6º nó `NOTIFICATION_SENT` no F6 (default = 5 nós; opção deferida ao `@sm`/`@dev`).

---

**Authority:** Aria (Architect) — designada por `@aiox-master` para remoção de componente, re-escopo de fronteira de integração e seleção de tecnologia. Detalhe de dados (decomissionar o PostgreSQL do n8n; dump de cortesia) delegável a **`@data-engineer`** (matriz de autoridade).
**Review cycle:** Imutável durante o épico da Final. Mudanças → nova ADE que a supersede.

## Change Log

| Date | Author | Description |
|---|---|---|
| 2026-06-30 | @architect (Aria) | **ADE-008 criada — supersede ADE-006.** Formaliza as 3 decisões do owner (2026-06-30): (1) pós-compra **inline** na `PurchaseConsumerFunction` (mock + trace correlacionado), decomissionando o Container App do n8n + o PostgreSQL gerenciado; (2) chatbot **só Fase A** (7 sentidos read-only, Story 2.8 Done) — dropa a "mão" `criar_alerta_ingresso`, o "Agente 2 / AI Agent node do n8n" e a Fase C; (3) formato hands-on. 6 invariantes. Regra de ouro **preservada e trivializada** (sem camada de ação); split sentidos/mãos sobrevive só nos **sentidos**; ADE-006 Inv 4 (mãos=n8n+AI Agent) **removida**, Inv 7 (leste-oeste) e a anti-alucinação "verificar nós n8n" **MOOT**; Fases B e C **dropadas**. F6 **6 → 5 nós** (remove `N8N_WEBHOOK_TRIGGERED`, motor inalterado) + **doc-fix M-1** (ADE-000 Inv 5 / AC-4: corpo+BeginScope, não `ApplicationProperties`). Preservadas: **ADE-004** (gateway guardião), **ADE-007** (identidade CIAM), **ADE-000** (invariantes), **ADE-002 exceto Inv 4** (tag n8n = MOOT). Débitos de legado (`N8nWebhookNotifier`, `AlertWebhookNotifier`, `criar_alerta_ingresso`, `infra/phase-04/`, `deploy-phase-04.yml`, App Settings `N8N_*`) apontados para `@dev`/`@devops`; decomissionar Postgres delegado a `@data-engineer`. Todas as afirmações de código verificadas na fonte da verdade (§Verificação). |

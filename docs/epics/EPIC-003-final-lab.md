# EPIC-003 — "A Final": último lab do Living Lab + descomissionamento do n8n (ADE-008)

> **Owner:** Morgan (PM) · **Status:** 📝 **DRAFT** · **Created:** 2026-06-30 · **Predecessor:** EPIC-002 (Living Lab Workshop — F1–F6 implementados; Quartas/Story 2.11 Done e validada em PRD)
> **Gate de arquitetura (pré-condição dura):** [ADE-008 — Descomissionar o n8n na Final](../architecture/ade-008-final-decommission-n8n.md) ✅ Accepted (supersede ADE-006)
> **Target event:** Aula "A Final" — fechamento do workshop Living Lab Azure-Native durante a Copa do Mundo FIFA 2026
> **Estimated class time (aluno):** ~**5,5–7,5h** (sessão única — F5 chatbot + F6 visualizer hands-on; **menor que as Quartas** porque o n8n e as Fases B/C saíram — ver §Dimensionamento)
> **Estimated squad build effort:** ~30–37h (6 stories, ver tabela)
> **Source blueprint:** [docs/workshops/2026-blueprint-living-lab-azure.md](../workshops/2026-blueprint-living-lab-azure.md) (F5/F6)

---

## Decisão estrutural: por que um ÉPICO PRÓPRIO (EPIC-003) e não o fechamento do EPIC-002 (2.12+)

> `[AUTO-DECISION]` **A Final é EPIC-003 (épico próprio), predecessor EPIC-002 — NÃO stories 2.12+.**

**Decisão:** a Final vira **`EPIC-003-final-lab`**, com stories numeradas **3.x**. Numeração **proposta** aqui; a confirmação final é de **@sm/@po** (autoridade de criação/backlog de stories).

**Justificativa (grounded):**

| Critério | EPIC-003 próprio | Fechar EPIC-002 (2.12+) |
|---|---|---|
| **Gate próprio** | ✅ A Final tem seu **próprio gate de arquitetura** — a **ADE-008**, que **supersede a ADE-006**. Um novo baseline arquitetural (remoção de uma camada inteira, o n8n) é fronteira natural de épico. | EPIC-002 fechou sobre ADE-004/ADE-007; reabri-lo com um ADE que supersede outro embaralha o baseline. |
| **Natureza do trabalho** | ✅ Não é "mais um lab": é **re-arquitetura** (pós-compra inline, F6 6→5 nós, limpeza de legado) **+ 2 labs hands-on** (F5, F6) **+ materiais** **+ um débito de identidade** (JIT CIAM). Ciclo de entrega multi-story. | — |
| **Precedente honesto (contra-argumento)** | As Quartas foram **1 story (2.11) dentro do EPIC-002** — não um épico. **Por que a Final difere:** Quartas foi **empacotamento** (reusou F2/F3 já implementados, só trocou a authority CIAM, zero re-arquitetura). A Final **muda o código** de F4/F6 e **remove um componente**, sob um ADE que supersede outro. Empacotamento ⇒ story; re-arquitetura multi-story com gate próprio ⇒ épico. | Seguir cegamente o precedente Quartas ignoraria que o escopo da Final é materialmente maior e cross-cutting. |
| **Encerramento** | ✅ EPIC-002 já tem `EPIC-002-RUN-LOG.md` e sua narrativa de 6 fases fechada; a Final consolida e re-arquiteta por cima — melhor como novo ciclo. | Reabrir um épico com RUN-LOG polui seu encerramento. |

**Trade-off aceito:** EPIC-003 **modifica deliverables do EPIC-002** (re-arquiteta F4/F6, empacota F5/F6). Isso é consistente com o padrão **brownfield cumulativo** do projeto (EPIC-002 já estendeu EPIC-000/001). Um épico que altera código de um épico anterior é legítimo aqui.

---

## Motivação

As Quartas (Story 2.11, F2/F3) foram **entregues e validadas rodando em PRD** (2026-06-29/30). Resta **um único lab** na trilha do owner: **"A Final"** — o fechamento do Living Lab. As capacidades que a Final apresenta — **F5 (chatbot/MCP/Gemini)** e **F6 (Flow Visualizer)** — **já foram implementadas** (Stories 2.5, 2.6, 2.8 **Done**), mas:

1. **O lab da Final ainda não foi construído** como produto de workshop entregável (padrão validado Oitavas/Quartas: guia consolidado + PPT + draw.io + quiz).
2. **O stack precisa de uma re-arquitetura** antes de virar lab: o owner decidiu (2026-06-30) **remover o n8n**. A **ADE-008** formaliza as 3 decisões e é o **gate** deste épico.

A Final entrega a experiência mais "AI-native" do workshop — um chatbot que **consulta** o estado real da Copa (só sentidos, read-only) e um visualizador que anima a **jornada de uma compra atravessando os microsserviços** — sobre uma arquitetura **mais enxuta** (sem a camada de orquestração externa que o n8n representava).

**Diferenciação vs os labs anteriores:**
- **Oitavas (F1):** mensageria + serverless (Service Bus + Functions).
- **Quartas (F2/F3):** borda + identidade (Gateway YARP + CIAM dual-issuer).
- **Final (F5/F6):** **IA + observabilidade** (MCP/chatbot read-only + correlation-ID visualizer), com **descomissionamento** de um componente (n8n) como lição de simplificação arquitetural.

## Escopo (in scope)

Derivado da ADE-008 (Invariantes 1–6) + backlog do @po + memórias de produto/dados. **Nada além disto.**

- **Re-arquitetura da orquestração pós-compra (ADE-008 Inv 3, 4, 5):**
  - Pós-compra **inline** na `PurchaseConsumerFunction` (mock HTTP/log + trace correlacionado), **sem n8n** — preservando ADE-000 Inv 4 (idempotência: notifica só em `Inserted`) e Inv 5 (`correlationId`).
  - **Remoção do n8n** como componente (Container App + **PostgreSQL gerenciado**) e como camada de ação/orquestração.
  - **F6 Flow Visualizer: 6 → 5 nós** (remove `N8N_WEBHOOK_TRIGGERED`; motor trace-driven inalterado).
  - **Doc-fix M-1** (ADE-000 Inv 5 / AC-4 hop 2: `correlationId` viaja no **corpo + `ILogger.BeginScope`**, não em `ApplicationProperties`).
- **F5 lab hands-on:** chatbot **só Fase A** (7 sentidos read-only, Story 2.8 Done) sobre o estado real da Copa — empacotado como lab entregável.
- **F6 lab hands-on:** Flow Visualizer (5 nós) — empacotado como lab entregável.
- **Limpeza de legado n8n:** código (`N8nWebhookNotifier`/`IN8nWebhookNotifier`/`N8nWebhookPayload`; `AlertWebhookNotifier`/`IAlertWebhookNotifier`/`AlertPayloads`; tool `criar_alerta_ingresso` em `FifaActionTools`) + infra (`infra/phase-04/`, `.github/workflows/deploy-phase-04.yml`) + App Settings (`N8N_WEBHOOK_URL`, `N8N_ALERT_WEBHOOK_URL`) + testes (`N8nWebhookNotifierTests`, partes de `FifaActionToolsTests`/`PurchaseConsumerFunctionTests`).
- **Débito JIT provisioning CIAM** (débito #4 do @po): usuário nato-CIAM (não migrado do v1) precisa de provisionamento `users`/`entra_oid` no primeiro login (endpoint `/api/v2/me` — groundwork coluna+índice já existe). **Identidade — governado por ADE-007, NÃO depende da ADE-008.** Avaliado como story própria (paralela).
- **Materiais didáticos** (padrão validado): guia consolidado estilo Oitavas/Quartas (`docs/runbooks/final-*-guide.md`) + PPT (Claude for PowerPoint) + diagrama draw.io (ícones Azure reais) + quiz fácil (Google Forms, **fora do repo**). Inclui **re-narrar** a etapa pós-compra como "a Function orquestra o pós-compra" (não mais "automação no-code").

## Fora de escopo (out of scope)

Reservado para épico posterior, decisão nova ou explicitamente dropado pela ADE-008:

- **Fase B — a "mão" `criar_alerta_ingresso` + AI Agent node do n8n** (Story 2.9): **DROPADA** pela ADE-008 (decisão 2). Vira legado a aposentar.
- **Fase C — jornada agêntica "dois agentes" com n8n:** **DROPADA** (ADE-008). Não é entregue.
- **Qualquer novo sink de "alertas"** (tabela `alerts` em Azure SQL + ação idempotente): reabriria a regra de ouro → exige **ADE própria**. Débito futuro, não a Final.
- **Substituir o n8n por outro orquestrador** (Logic Apps / Power Automate): **rejeitado** pela ADE-008 (Alt 1) — troca um orquestrador por outro, não realiza a simplificação.
- **Reescrever backend Node em .NET · mobile · i18n · pagamento real · multi-currency** (herdado do EPIC-002).
- **Resolver os débitos de qualidade** frontend-0-testes e CIAM-e2e-sem-cobertura — **sinalizados** (ver §Riscos e §Débitos), decisão de resolver é de @sm/@po.

## Restrições firmes (vacas sagradas)

- ✅ **Regra de ouro (ADE-008 Inv 1):** o chatbot **nunca escreve direto no banco**. Agora **trivial** — não há camada de ação; McpServer é **só leitura** (`FifaQueryRepository` só `SELECT`).
- ✅ **Sem n8n** — nenhuma story pode reintroduzir n8n ou um orquestrador equivalente.
- ✅ **ADEs preservadas (ADE-008 Inv 6):** ADE-004 (gateway YARP guardião), ADE-007 (identidade CIAM dual-issuer), ADE-000 (idempotência + correlationId), ADE-002 (pins .NET, exceto a tag n8n que fica MOOT) — **inalteradas**.
- ✅ **Fluxo v1 intocado** (Node/Express + bcrypt+JWT) — v2 é paralelo (ADE-000 Inv 1).
- ✅ **Provisioning Portal-first** + custo ~US$0 (agora **menor**: sai o Container App do n8n + o PostgreSQL gerenciado ~US$13/mês).
- ✅ **Governança TFTEC de entrega do lab:** branch curada `lab-*` no upstream + PR no fork do aluno (padrão validado nas Oitavas/Quartas).

## Pré-condições do épico (pré-flight)

- ⛔ **GATE — ADE-008 Accepted** (✅ satisfeito). `@pm`/`@sm` **não** draftam stories de re-arquitetura/labs antes da ADE-008 existir. Ela existe e está aceita → gate liberado.
- ⛔ **Stories 2.5, 2.6, 2.8 Done** (✅ — F5/F6/Fase A implementados; base a empacotar/re-arquitetar).
- ⛔ **EPIC-002 Quartas (2.11) Done e validada em PRD** (✅ — a Final parte do estado pós-Quartas).

## Success criteria

| # | Critério | Verificação |
|---|---|---|
| SC-1 | Aluno completa F5 (chatbot só-sentidos) + F6 (visualizer 5 nós) na sessão da Final (~5,5–7,5h) | Cronômetro por bloco; checkpoint ao fim de cada bloco |
| SC-2 | **Regra de ouro provada:** o chatbot responde consultando o estado real da Copa e **nunca** grava no banco (só `SELECT`) | Inspeção + smoke: nenhuma tool `ReadOnly=false` em escopo |
| SC-3 | F6 mostra a compra atravessando **exatamente 5 nós** (Gateway → Entry → Service Bus → Consumer → SQL) em < 30s | Demo individual: "bolinha" atravessa 5 nós |
| SC-4 | **n8n 100% descomissionado:** zero referência a n8n em código/infra/App Settings; Container App + PostgreSQL removidos | ADE-008 §Validation checklist (8 itens) + grep `N8N_`/`n8n` = 0 em escopo |
| SC-5 | Custo total do evento **ainda ~US$0** e **menor que EPIC-002** (sai n8n Container App + PostgreSQL ~US$13/mês); custo por aluno ≤ US$15 | Azure Cost Management + Budget Alert |
| SC-6 | Pacote didático da Final entregue (guia + PPT + draw.io + quiz) no padrão Oitavas/Quartas | Checklist de artefatos + revisão @pm |
| SC-7 | NPS > 8/10 entre alunos da Final | Pesquisa pós-evento |

## Stories

> **Executor primário** = executor padrão da task; **@sm/@po** confirmam numeração e granularidade. Branch destino "@devops confirma o slot" (padrão AC-1 da 2.11).

| # | ID | Título | Branch destino | Estimativa (build) | Executor primário | Quality gate |
|---|---|---|---|---|---|---|
| S1 | **3.1** | Re-arquitetura ADE-008 (código): pós-compra inline + F6 6→5 nós + remoção do código n8n + doc-fix M-1 | `phase-07-final-rearch` (novo; @devops confirma) | **~6h** | @dev | @architect |
| S2 | **3.2** | Descomissionamento de infra/runtime do n8n: Container App + PostgreSQL + App Settings `N8N_*` + workflow/IaC | `phase-07-final-rearch` | **~2–3h** | @devops (+ @data-engineer p/ Postgres) | @architect |
| S3 | **3.3** | F5 lab hands-on: chatbot "só sentidos" (Fase A, 7 tools read-only) sobre o estado real da Copa | `lab-a-final` (curada; @devops confirma) | **~5h** | @dev (+ @analyst materiais) | @architect |
| S4 | **3.4** | F6 lab hands-on: Flow Visualizer (5 nós) — jornada de compra animada por correlation-ID | `lab-a-final` (curada) | **~5h** | @dev (+ @analyst materiais) | @architect |
| S5 | **3.5** | JIT provisioning CIAM: usuário nato-CIAM provisionado no 1º login (`/api/v2/me`) — débito #4, ADE-007 | `phase-07-final-rearch` ou dedicada (@devops confirma) | **~4h** | @dev (+ @data-engineer schema) | @architect |
| S6 | **3.6** | Materiais didáticos da Final (transversal): guia consolidado + PPT + draw.io + quiz Google Forms | paralelo (docs/runbooks + docs/workshops) | **~10h spread** | @analyst | @pm |

**Total build estimado:** ~30–37h de squad (executável com paralelismo — ver dependências).

> `[AUTO-DECISION]` **6 stories (3.1–3.6)** — mapeiam 1:1 aos bullets do escopo sem over-splitting. **Opções deixadas a @sm** (autoridade de story): (a) **dividir 3.1** em 3.1a (Functions: pós-compra inline + remoção n8n code) e 3.1b (F6 5 nós) se o tamanho combinado incomodar — o default é **unificado** (uma mudança arquitetural, um gate ADE-008); (b) **fundir 3.3+3.4** numa única "Final lab" story seguindo o precedente Quartas (1 lab = 1 story) — o default é **duas** (F5 e F6 são superfícies técnicas distintas: chatbot/MCP vs visualizer/SignalR).

### Padrão de artefatos por story de lab (S3.3, S3.4)

Segue o padrão validado nas Oitavas/Quartas (não os 6 artefatos "de fase" originais do EPIC-002, mas o **pacote consolidado** que substituiu):

| Artefato | Onde | Audiência |
|---|---|---|
| Guia consolidado do aluno (estilo Oitavas/Quartas) | `docs/runbooks/final-*-guide.md` | Aluno (segue ao vivo) |
| Branch curada `lab-a-final` + PR→main no fork | upstream TFTEC + fork | Aluno (fluxo 100% web) |
| Slides / PPT (Claude for PowerPoint) | material da Final (S3.6) | Apresentação |
| Diagrama draw.io (ícones Azure reais) | arquitetura da Final (S3.6) | Aluno/facilitador |
| Quiz fácil (Google Forms) | **fora do repo** (S3.6) | Aluno (avaliação) |

## Dependências entre stories

```
   ┌─ ADE-008 (GATE, ✅ Accepted) ─┐
   ▼                               │
3.1 (re-arch código: inline + F6 5 nós + remove n8n code + M-1)   ← linchpin
   ├─> 3.2 (descomissiona infra/Postgres/App Settings do n8n)      [após o código parar de referenciar n8n]
   ├─> 3.3 (F5 lab: chatbot só sentidos)                           [precisa do McpServer read-only, "mão" removida]
   └─> 3.4 (F6 lab: visualizer 5 nós)                              [precisa do código de 5 nós]

3.5 (JIT CIAM) ───────── paralela (ADE-007, independente da ADE-008)
3.6 (materiais) ──────── transversal (segue a forma pós-3.1 p/ narrar a arquitetura certa)
```

- **3.1 é o linchpin:** realiza a ADE-008 no código. 3.2/3.3/3.4 dependem dele.
- **3.2 não bloqueia os labs** — enquanto o n8n não é derrubado, ele fica apenas ocioso (os webhooks já são fail-open/no-op quando a URL está vazia). Pode rodar em paralelo com 3.3/3.4.
- **3.5 é independente** da cadeia n8n (identidade). Pode ser draftada/executada a qualquer momento (P2 no backlog do @po).
- **3.6 é transversal**, mas deve **seguir a forma de 3.1** para descrever a arquitetura re-narrada corretamente.

## Artefatos pré-existentes que cada story alavanca

| Story | Reusa |
|---|---|
| 3.1 | `src/Fifa2026.V2.Functions/Functions/PurchaseConsumerFunction.cs` (o `case Inserted` já é o ponto de extensão — Inv 3); `src/Fifa2026.V2.FlowEvents/` (motor trace-driven: `AppInsightsFlowEventRepository`, `TraceEventMapper`, `FlowHub` — só a lista de nós muda); `FlowEventType.cs` (enum 6→5) |
| 3.2 | `infra/phase-04/` (a remover); `.github/workflows/deploy-phase-04.yml` (a remover); App Settings `N8N_*` dos serviços deployados; PostgreSQL gerenciado (dump + drop — @data-engineer) |
| 3.3 | Story 2.8 Done (7 sentidos read-only: `FifaTicketTools`/`FifaQueryRepository`); McpServer atrás do gateway (ADE-004); Gemini (frontend `gemini.ts`); padrão de lab curado `lab-quartas-de-final` |
| 3.4 | Story 2.6 Done (F6: SignalR + `FlowDiagram` frontend + framer-motion); App Insights (desde F1); motor de F6 inalterado |
| 3.5 | `users.entra_oid` + índice UNIQUE filtrado (groundwork da Quartas, `phase-04-ciam-link.sql`); pipeline `oid → X-Entra-OID → entra_oid`; ADE-007 |
| 3.6 | `docs/runbooks/quartas-f2-portal-guide.md` (molde do guia consolidado); pacote validado Oitavas/Quartas (PPT + draw.io + quiz) |

## Riscos e mitigações

| # | Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|---|
| 1 | **Tempo/fadiga na aula** (o mesmo risco que estourou nas Quartas ~7,5–9,5h) | Média | Médio | **MITIGADO PELO ESCOPO REDUZIDO:** a Final **encolheu** vs o temido "~10-12h" — **saiu o n8n (F4)** e **saíram as Fases B/C**. Restam só F5 (só-sentidos) + F6 (visualizer). Estimativa honesta de aula: **~5,5–7,5h** (sessão única, ver §Dimensionamento). Hands-on focado no que resta; sem provisionar orquestrador ao vivo. |
| 2 | **Gemini 2.5-flash shutdown (out/2026)** afeta F5 | Média | Alto | Avaliar migração p/ `gemini-2.5-flash-lite` ou `3.5-flash` **antes** do out/2026 (`@architect` decide o pin; `gemini.ts` runtime já é 2.5-flash — só o comentário do pin está velho). Fallback documentado no material da Final. |
| 3 | **JIT CIAM pode exigir emenda à ADE-007** (não só implementação) | Média | Médio | 3.5 abre com `@architect` confirmando se o caminho `/api/v2/me` + JIT insert cabe na ADE-007 vigente ou precisa de emenda. Groundwork (coluna+índice) já existe → risco de schema é baixo; o risco é de contrato/decisão. |
| 4 | **Legado n8n mal removido** deixa código morto/inconsistência | Baixa | Médio | ADE-008 §Validation (8 itens) é o checklist de 3.1/3.2. Enquanto não removido, é fail-open tolerado (não quebra compra nem chat). `@architect` gate valida a remoção completa. |
| 5 | **Frontend com ZERO testes automatizados** (débito recorrente) — 3.3/3.4 tocam UI de chat/visualizer | Média | Médio | **SINALIZADO, não necessariamente resolvido nesta épica.** Candidato a story transversal de testes de frontend OU AC mínima em 3.3/3.4 — decisão de @sm/@po (backlog #6). |
| 6 | **CIAM e2e sem cobertura automatizada** (login/JIT testados só à mão) | Média | Médio | **SINALIZADO.** 3.5 deve incluir ao menos teste do JIT insert (unit); e2e do login CIAM segue manual (limitação conhecida). |
| 7 | Cold start de Functions / SignalR trava demo ao vivo | Média | Baixo | Warmup 5min antes do bloco (herdado do EPIC-002). |
| 8 | Custom domain / secrets do fork incompletos (CI/CD) | Baixa | Baixo | Deploys manuais validados (padrão atual); `@devops` completa secrets se automatizar (débito #5 do @po, não-bloqueante). |

## Compatibility Requirements

- [x] APIs v1 (Node/Express) intocadas — v2 paralelo.
- [x] Mudanças de schema backward-compatible — 3.5 só usa `users.entra_oid` já existente (aditivo idempotente); nenhuma DDL destrutiva.
- [x] Remoção do n8n **não quebra** compra nem chat — webhooks já são fail-open/no-op quando vazios (verificado, ADE-008 Rationale).
- [x] F6 muda de 6→5 nós sem tocar o motor (Kusto + SignalR agnósticos à lista de nós).
- [x] Impacto zero no fluxo v1; medível no v2 (App Insights).

## Decisões fechadas

| # | Decisão | Fonte / Ref |
|---|---|---|
| 1 | **Estrutura = épico próprio (EPIC-003), stories 3.x** | `[AUTO-DECISION]` @pm (§Decisão estrutural) — confirmação de numeração por @sm/@po |
| 2 | **Pós-compra = Function inline + trace correlacionado** (mock HTTP/log); decomissiona Container App do n8n + **PostgreSQL** | **ADE-008 Inv 3** + decisão do owner 2026-06-30 |
| 3 | **Chatbot = só Fase A** (7 sentidos read-only, Story 2.8 Done); **dropa** a "mão" `criar_alerta_ingresso`, o "Agente 2"/AI Agent node do n8n e a Fase B/C | **ADE-008 Inv 1/2** + decisão do owner 2026-06-30 |
| 4 | **Formato = aluno constrói tudo hands-on** (F5 + F6) | **ADE-008** decisão 3 do owner 2026-06-30 |
| 5 | **n8n REMOVIDO** como componente e camada de orquestração/ação | **ADE-008 Inv 4** + decisão do owner 2026-06-30 |
| 6 | **F6 = 5 nós** (remove `N8N_WEBHOOK_TRIGGERED`; `SQL_INSERTED` renumerado 5→4; motor inalterado) | **ADE-008 Inv 5** |
| 7 | **Doc-fix M-1:** `correlationId` viaja no **corpo + `ILogger.BeginScope`**, não em `ApplicationProperties` (ADE-000 Inv 5 / AC-4) | **ADE-008 Inv 3** (M-1); @po aplica no doc-fix |
| 8 | **ADE-004/ADE-007/ADE-000 inalteradas; ADE-002 Inv 4 (tag n8n) = MOOT** | **ADE-008 Inv 6** |
| 9 | **JIT CIAM é identidade (ADE-007), NÃO depende da ADE-008** — story própria paralela | Backlog @po débito #4; ADE-008 §Impact |
| 10 | **Substituto no-code (Logic Apps/Power Automate) rejeitado** — não realiza a simplificação | **ADE-008 Alt 1** |

## Decisões pendentes (carry-forward para o draft das stories)

| Decisão | Quando resolver | Responsável |
|---|---|---|
| Forma concreta da notificação inline: **HTTP mock vs só log** (ambas honram a decisão do owner) | Draft de 3.1 | @sm/@dev |
| Manter um 6º nó `NOTIFICATION_SENT` no F6 (visível) OU dobrar no Consumer | Draft de 3.1/3.4 — **default ADE-008 = 5 nós** | @sm/@dev |
| Pin do Gemini (2.5-flash → 2.5-flash-lite/3.5-flash antes do out/2026) | Draft de 3.3 | @architect |
| JIT CIAM: ADE-007 vigente cobre `/api/v2/me`+JIT ou precisa de emenda? | Abertura de 3.5 | @architect |
| Testes de frontend: story própria transversal OU AC dentro de 3.3/3.4? | Backlog da Final | @sm/@po |
| Labs 3.3/3.4: manter separadas OU fundir em 1 "Final lab" story (precedente Quartas) | Draft | @sm |
| Formato de calendário da Final (encaixe na trilha do workshop) | Pré-evento | @pm |

## Dimensionamento (honesto) — tempo de aula da Final

> **A Final é MENOR que as Quartas.** As Quartas somaram ~7,5–9,5h porque unificaram gateway + identidade CIAM dual-issuer + migração hands-on. A Final **perdeu duas fontes de peso**: o **n8n** (provisionar Container App + PostgreSQL + montar workflow ao vivo) e as **Fases B/C** (a "mão" agêntica + jornada dois-agentes). Sobra **F5 (chatbot só-sentidos)** + **F6 (visualizer)**.

| Bloco | Conteúdo | Tempo estimado | Modo |
|---|---|---|---|
| 0 | Abertura: recap da jornada v2, o que é MCP, regra de ouro (chatbot read-only) | ~30min | Expositivo |
| 1 | **F5 hands-on:** deploy McpServer + chatbot + Gemini; exercitar os 7 sentidos sobre o estado real da Copa | ~2,5–3,5h | Hands-on |
| 2 | **F6 hands-on:** deploy FlowEvents + SignalR + visualizer; ver a compra atravessar 5 nós | ~2–3h | Hands-on |
| 3 | Retro + Q&A: "por que o chatbot nunca escreve?", "onde foi o n8n?" (lição de simplificação), fecho do Living Lab | ~30min | Conversa |
| | **Total** | **~5,5–7,5h** (sessão única) | |

**Risco de tempo/fadiga (Risco #1):** registrado honestamente — é o mesmo que estourou nas Quartas. **Mitigação real:** o escopo **encolheu** (n8n + Fases B/C fora), então o hands-on cabe numa sessão única sem o estouro das Quartas. Sem provisionar orquestrador ao vivo (o maior sorvedouro de tempo do design antigo).

## Débitos sinalizados (não necessariamente resolvidos nesta épica)

- **Frontend com ZERO testes automatizados** (React/Vite) — recorrente; candidato a story transversal se 3.3/3.4 mexerem fundo na UI (backlog @po #6).
- **CIAM e2e sem cobertura automatizada** — login/JIT validados à mão; 3.5 cobre ao menos o JIT insert em unit test.
- **Fase B (Story 2.9) runtime nunca fechou em PRD** — vira **MOOT** com a remoção do n8n; código a aposentar em 3.1.
- **Fase C órfã (sem story)** — **DROPADA** pela ADE-008; não será draftada.
- **CI/CD do fork incompleto** (secrets `AZURE_CREDENTIALS`/`PHASE06_SIGNALR_CONNECTION_STRING`; CodeRabbit sem seat) — não-bloqueante; deploys manuais validados (backlog @po #5).
- **ADE-002 Inv 4 (tag n8n)** a anotar como MOOT (registro de doc por @architect, fora desta task).

## Stakeholders

- **Owner do evento:** Guilherme Prux Campos (guilherme.campos@tftec.com.br)
- **Audiência:** alunos da TFTEC (pós-graduação) + devs convidados (polyglot, background cloud)
- **Squad:** @pm (Morgan) → @sm (River) → @po (Pax) → @dev (Dex) + @analyst (Atlas) materiais + @architect (Aria) gate ADE-008 + @data-engineer (Dara) Postgres + @devops (Gage) CI/CD/teardown
- **Nota:** a **Semifinal é de outro instrutor (Raphael)** — fora do escopo de planejamento desta épica.

## Próximos passos (PM → SM)

1. ✅ **ADE-008 Accepted** (gate liberado) — `docs/architecture/ade-008-final-decommission-n8n.md`
2. ✅ **EPIC-003 criado** — este documento
3. ➡️ **@sm (River)** drafta as stories em `docs/stories/3.{1..6}.story.md` (confirma numeração 3.x com @po). **Ordem recomendada:**
   - **1º — 3.1** (linchpin; realiza a ADE-008 no código; usa o §Validation da ADE-008 como esqueleto de AC).
   - **2º — 3.2** (descomissiona infra/Postgres — mecânico; depende de 3.1).
   - **3º — 3.3 e 3.4** (labs hands-on, em paralelo; dependem da forma de 3.1).
   - **Em paralelo — 3.6** (materiais; segue a forma de 3.1 para narrar a arquitetura re-narrada).
   - **Quando couber — 3.5** (JIT CIAM; independente; abrir com @architect confirmando ADE-007).
4. Cada story deve ter: **Status Draft** · Story do ponto de vista do **aluno** (labs) ou do **squad** (re-arch) · **ACs rastreáveis** (Art. IV — cada AC cita ADE-008 Inv X / ADE-007 / decisão do owner) · Tasks executáveis · Dev Notes com refs · Validation (smoke).
5. **@po (Pax)** valida cada story no checklist de 10 pontos antes de Ready.
6. **@architect (Aria)** é o quality gate de 3.1–3.5 (ADE-008 §Validation; ADE-007 p/ 3.5).
7. **@devops (Gage)** confirma slots de branch, orquestra CI/CD, faz o teardown do n8n (3.2) e a entrega da branch curada `lab-a-final` + PR no fork (governança TFTEC).
8. **@data-engineer (Dara)** executa o decommission do PostgreSQL (dump de cortesia + drop) em 3.2 e confirma o schema do JIT em 3.5.

## Definition of Done (Epic-level)

- [ ] Stories 3.1–3.6 **Done** com QA gate **PASS**.
- [ ] **ADE-008 §Validation checklist (8 itens) 100% satisfeito:** `PurchaseConsumerFunction` sem `IN8nWebhookNotifier` (inline); notificação com trace correlacionado (corpo+BeginScope); n8n Container App + PostgreSQL removidos; App Settings `N8N_*` removidos; F6 = 5 nós; McpServer read-only (7 sentidos intactos); legado aposentado; ADE-002 Inv 4 anotada MOOT; ADE-004/007/000 inalteradas (exceto doc-fix M-1).
- [ ] **Doc-fix M-1 aplicado** (ADE-000 Inv 5 / AC-4: corpo+`BeginScope`, não `ApplicationProperties`) — @po.
- [ ] **Lab da Final entregue** na branch curada `lab-a-final` (upstream TFTEC) + fluxo de PR→main no fork do aluno validado.
- [ ] **Pacote didático da Final** entregue (guia consolidado + PPT + draw.io + quiz Google Forms) no padrão Oitavas/Quartas.
- [ ] **JIT CIAM (3.5)** entregue OU explicitamente diferido com decisão de @architect/@po registrada.
- [ ] **Cost report** ~US$0 e **menor que EPIC-002** (n8n Container App + PostgreSQL removidos) + ≤ US$15/aluno.
- [ ] Grep de regressão: `N8N_` / `n8n` / `criar_alerta_ingresso` = **0 ocorrências** em código/infra/App Settings em escopo.

---

## Change Log

| Date | Version | Description | Author |
|---|---|---|---|
| 2026-06-30 | 0.1 | **EPIC-003 criado** — "A Final" como épico próprio (predecessor EPIC-002), sob o gate **ADE-008** (descomissionar o n8n). 6 stories propostas (3.1 re-arch código · 3.2 descomissiona infra/Postgres · 3.3 F5 lab só-sentidos · 3.4 F6 lab 5 nós · 3.5 JIT CIAM · 3.6 materiais). Decisão estrutural (EPIC-003 vs 2.12+) justificada (`[AUTO-DECISION]`: gate próprio + re-arquitetura multi-story vs empacotamento das Quartas). Escopo derivado da ADE-008 (Inv 1–6) + backlog @po (6 débitos) + memórias de produto/dados. Dimensionamento honesto: **aula ~5,5–7,5h** (menor que Quartas — n8n + Fases B/C fora). Riscos-chave: tempo/fadiga (mitigado pelo encolhimento), Gemini shutdown out/2026, JIT pode exigir emenda ADE-007, frontend 0 testes (sinalizado). Numeração 3.x a confirmar por @sm/@po. Nada inventado além das fontes (Art. IV). | @pm (Morgan) |

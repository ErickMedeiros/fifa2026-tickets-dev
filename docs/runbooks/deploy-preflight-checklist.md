# Deploy Preflight Checklist — Gateway (Container App) + Admin Workforce

> **Origem:** Story 4.4 (EPIC-004, Frente B) — consolida os **5 gotchas** recorrentes do deploy
> em produção das Quartas, hoje corretos mas **espalhados** em texto narrativo no runbook
> `quartas-f2-portal-guide.md`. Aqui viram uma **lista explícita, nomeada e reutilizável** para
> não reincidirem.
>
> **Quando usar:** ANTES de todo deploy real do gateway/backend (workflows `lab-quartas-de-final.yml`,
> `deploy-phase-*.yml`) e como **insumo direto** do Doctor `acao=check` da Story 4.5.
>
> **Fonte da verdade (Art. IV):** cada item cita a(s) linha(s) de `docs/runbooks/quartas-f2-portal-guide.md`
> onde o gotcha foi documentado + o troubleshooting em uso (Apêndice C do runbook).

---

## Os 5 gotchas (checklist)

| # | Item | Verificar | Sintoma se faltar | Fonte (runbook) |
|---|------|-----------|-------------------|-----------------|
| 1 | **RBAC do Service Principal no RG do gateway** | O SP das GitHub Actions (OIDC/`azure/login`) tem role (Contributor ou equivalente) **no Resource Group do gateway** — não só no RG original. Ao recriar o gateway num RG diferente (ex.: VNet pura recriada em PRD), reatribuir o RBAC. | Step de deploy do Actions falha com `AuthorizationFailed` / `does not have authorization to perform action` sobre o Container App. | Troubleshooting de deploy PRD (Quartas); Fase 5/6 do runbook (SP + `azure/login`). |
| 2 | **ACR conectado em Registries do Container App** | Container App → **Settings → Registries** aponta para `cr<sufixo>.azurecr.io` (Admin Credentials). Sem isso o Container App não puxa a imagem real (fica no placeholder). | Deploy "sobe" mas o app roda a imagem quickstart placeholder (não a do gateway) → comportamento inesperado / `ImagePullBackOff`. | L221 (Registries → Add), L260 (checkpoint "**ACR conectado**"). |
| 3 | **`ingress.targetPort = 8080`** | Ingress do Container App com **Target port = 8080** (a imagem expõe `EXPOSE 8080` + `ASPNETCORE_URLS=http://+:8080`). | **502 em TODA chamada** (o ingress bate numa porta onde nada escuta). | L213 (Target port 8080), L217 (⚠️ "**8080 é crítico… Qualquer outro valor = 502**"), L476 (Apêndice C: "502 em toda chamada → targetPort ≠ 8080"). |
| 4 | **Health probes na porta 8080** | Liveness/readiness probes (quando configurados) na **mesma porta 8080** do ingress/app. | Container marcado unhealthy / reciclado em loop mesmo com o app OK; ou `/health` só responde após cold start. | L217 (porta 8080 da imagem), L482 (Apêndice C: `/health` no 1º hit / cold start). |
| 5 | **Nome do segredo: `Gateway__AdminSharedSecret` == `GATEWAY_SHARED_SECRET`** | O **mesmo valor** do segredo forte está no App Setting do gateway `Gateway__AdminSharedSecret` **e** no Secret do backend `GATEWAY_SHARED_SECRET` (aplicado por `acao=backend`). Duplo underscore no gateway (`:` vira `__` em env var). | Admin **401/403 com token válido** (`roles:["Admin"]`): o `X-Gateway-Key` injetado pelo gateway não bate com o esperado pelo backend (`gatewayTrust.js`). | L236, L262 (handshake gateway↔backend), L479 (Apêndice C: "Admin 401/403 com token válido → usar o MESMO valor nos dois"). |

---

## Uso como preflight (marcar antes de cada deploy real)

- [ ] **1. RBAC** — SP das Actions tem role no **RG atual** do gateway (revalidar após recriar infra).
- [ ] **2. ACR** — Registries do Container App conectado a `cr<sufixo>.azurecr.io`.
- [ ] **3. Ingress** — Target port = **8080**.
- [ ] **4. Probes** — health probe(s) na porta **8080** (se configurados).
- [ ] **5. Segredo** — `Gateway__AdminSharedSecret` (gateway) == `GATEWAY_SHARED_SECRET` (backend), **mesmo valor**.

> Regra prática das Quartas: **#3 e #5 são os de maior recorrência** — um 502 universal aponta quase
> sempre para o item 3; um "admin autentica mas dá 403" aponta quase sempre para o item 5.

---

## Handoffs

- **Story 4.5 (Doctor `acao=check`):** este checklist é o **insumo direto** dos checks automatizados do
  Doctor — cada item acima vira uma verificação idempotente (ler estado do recurso e comparar com o
  esperado, sem assumir estado vazio). Ver AC-11/Task 5.3 da Story 4.4.
- **@devops (Bloco E / AC-12, Task 5.2):** auditar `lab-quartas-de-final.yml` e `deploy-phase-*.yml`
  contra os 5 itens e adicionar **guardas de idempotência** onde um passo hoje falharia (não ignoraria
  silenciosamente) ao rodar contra um recurso já configurado corretamente — no mínimo **ACR registries**
  (#2) e **ingress port** (#3). O escopo exato dos comandos/guardas é decisão de implementação de
  `@devops` (infra não é escrita nesta story de `@dev`/`@sm`).

---

## Nota do @architect (gate Story 4.4) — invariante de perímetro do `UseForwardedHeaders`

> Complemento **operacional** ao `ForwardLimit=1` do gateway (Story 4.4, Bloco C). **Não** é um 6º
> gotcha do checklist acima (o AC-11 fixa exatamente 5) — é uma **invariante de deploy** que o Doctor
> `acao=check` (Story 4.5) deve verificar.

- [ ] **6 (invariante). O gateway é alcançável SOMENTE via o ingress do Container Apps Environment (CAE).**
  O `UseForwardedHeaders` do gateway roda com `KnownNetworks`/`KnownProxies` **limpos** — confia no
  `X-Forwarded-For` apôsto pelo peer imediato (o ingress do CAE). Essa confiança só é segura se **nada
  além do ingress** alcança o container do gateway: se um cliente pudesse bater direto no container
  (porta exposta, IP público do pod, peering mal-configurado), forjaria o `X-Forwarded-For` e derrotaria
  a partição de rate-limit por IP real. **Verificar no deploy:** ingress do gateway = borda pública é o
  ingress (não o container) e o container não tem outra rota de entrada. O `ForwardLimit=1` (código) é a
  trava; esta invariante (deploy) é a premissa que a sustenta.

---

_Consolidado por @dev (Dex) — Story 4.4, 2026-07-01. Citações verificadas linha-a-linha contra
`docs/runbooks/quartas-f2-portal-guide.md` (em uso). Nota de perímetro adicionada por @architect (Aria)
no gate da Story 4.4._

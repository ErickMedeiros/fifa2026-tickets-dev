# ADE-007 — Identidade do cliente via Microsoft Entra External ID (CIAM); workforce restrito ao admin (supersede ADE-005)

> **Tipo:** Architecture Decision Entry (component substitution — provedor de identidade do cliente + reposicionamento do workforce)
> **Status:** ✅ Accepted · **SUPERSEDES ADE-005** · **v1.3.1** (v1.1 — Inv 3 emendada, dois eixos de identidade; **v1.2 — Inv 8 adicionada, JIT provisioning do usuário nato-CIAM**, insumo/desbloqueio da Story 3.5; **v1.3 — fence de esquema `CiamOnly` na Inv 8 (8.2) + reafirmação do framing base↔CIAM; Story 3.5 promovida a peça central da Final**; **v1.3.1 — gate da Story 3.5: forma validada do fence = `RequireAssertion` por issuer, não scheme-pinning**)
> **Date:** 2026-06-25 (v1.0) · **Amended:** 2026-06-25 (v1.1) · **Amended:** 2026-06-30 (v1.2 — Inv 8 JIT) · **Amended:** 2026-07-01 (v1.3 — fence `CiamOnly` + framing base↔CIAM)
> **Author:** Aria (Architect) · **v1.3 co-autoria:** Squad AIOX TFTEC
> **Scope:** EPIC-002 F2 "Quartas de Final" (Gateway YARP + Identidade do cliente) e fases que dependem da identidade do usuário (F5 chatbot, F6 visualizer). Materializa-se em `authV2.ts`, gateway `Program.cs`, App Settings e no roteiro de aula das Quartas. **(v1.2)** Passa também a governar o **provisionamento JIT** do cliente nato-CIAM (endpoint `/api/v2/me`, insumo da Story 3.5 do épico da Final) — ver Invariante 8.
> **Supersedes:** **ADE-005** (identidade via App Registration no tenant workforce + Easy Auth — premissa de atrito do External ID **revista**). Ver §"O que muda vs ADE-005".
> **Related:** ADE-000 (microsserviço paralelo — Inv 1 comparação lado-a-lado, Inv 2 schema delta idempotente, Inv 5 W3C Trace Context), ADE-003 (baseline PaaS + front em App Service), ADE-004 (gateway YARP valida o JWT — **preservada, issuer-agnóstica**), ADE-006 (identidade `oid` propagada como `X-Entra-OID` ao McpServer/n8n — **inalterada**), **`migration-v1-ciam-design.md`** (@data-engineer — mecanismo da migração de cadastro + decisão de schema `users.entra_oid`, insumo da Inv 3 v1.1 e Inv 6). **(v1.2)** **Story 3.5** (`docs/stories/3.5.story.md` — JIT provisioning do nato-CIAM; materializa a Inv 8; a story é **desbloqueada**, não editada por esta ADE — autoridade @sm/@po).
> **Rastreabilidade (Art. IV):** todas as decisões abaixo derivam de `docs/research/quartas-kickoff-handoff.md` (§3 decisões do owner, §4 decisão A/B, §7 constraints) e `docs/research/2026-06-25-identidade-external-id-quartas.md` (memo de identidade, §7 proposta de ADE). Itens sem fonte são marcados **"a confirmar com owner"**.

---

## Context

Esta ADE foi gerada a partir do kickoff das **Quartas de Final (F2)** com o owner (Guilherme Prux Campos) em **2026-06-25**, com base no memo de identidade de Atlas (@analyst) e nas decisões registradas no handoff (`quartas-kickoff-handoff.md` §3).

A **ADE-005** (2026-06-03) decidiu, num re-escopo anterior, **remover o Entra External ID** e usar o **tenant Entra ID workforce** do aluno (App Registration + Easy Auth / MSAL.js), porque o External ID "introduz atrito alto e desproporcional" — exigia tenant CIAM separado + user flows. A própria ADE-005 registrou honestamente o trade-off (Consequência negativa): *"workforce ≠ CIAM; o real B2C é o Entra External ID"*.

Duas coisas mudaram desde então e justificam reabrir a decisão:

1. **A premissa de atrito da ADE-005 caiu materialmente.** Hoje a Microsoft oferece um **trial tenant External ID sem subscription e sem cartão** (30 dias, até 10K objetos) + extensão VS Code + get-started guide que cria tenant + user flow + app de exemplo em minutos (memo §2.1/§4). O atrito que matou o CIAM em junho é **pré-provisionável pelo instrutor**, fora do relógio da aula.

2. **O caminho da ADE-005 está semanticamente errado para o cliente final.** O app de ingressos é **B2C consumer-facing**; o tenant workforce (`login.microsoftonline.com`) modela **funcionários/B2B**. O produto correto para clientes é o **Microsoft Entra External ID (CIAM)** — sucessor oficial do Azure AD B2C (memo §0/§1).

O owner (2026-06-25, handoff §3) decidiu **adotar o External ID para o cliente** e, diferente do que o memo recomendava como mínimo, **escolheu as opções mais "hands-on"**: além do cliente CIAM, **construir** o login de admin (workforce + App Roles) e **executar a migração** `users` v1 → CIAM como passo prático. Esta ADE formaliza essas decisões e dimensiona o esforço (§Dimensionamento & Recomendação A/B), porque o conjunto **provavelmente estoura as ~6h** de uma aula (handoff §4 — decisão A/B pendente do owner).

> **Esclarecimento de terminologia (vai para o material da aula, memo §1):** **Entra Connect** = ponte de sync AD on-prem → nuvem (irrelevante aqui); **Entra ID** = crachá de funcionário (workforce/B2B, `login.microsoftonline.com`); **Entra External ID** = cadastro de cliente (CIAM/B2C, `<tenant>.ciamlogin.com`). O comprador entra pelo External ID; o admin pelo workforce. Esse é o desenho **canônico** de produto B2C.

---

## Decision

Adotamos o pattern **"Identidade dois-mundos: cliente no Entra External ID (CIAM), admin no workforce; gateway YARP valida ambos os JWTs de forma issuer-agnóstica"**, com 7 invariantes. As decisões do owner (handoff §3) são:

| Dimensão | Decisão (owner, 2026-06-25) | Fonte |
|---|---|---|
| **Cliente (B2C)** | **Entra External ID (CIAM)** com **social login Google** pré-configurado pelo instrutor; **email + OTP** como fallback | handoff §3 |
| **Tenant CIAM** | **Trial 30 dias** (sem subscription/cartão, recriável por turma) | handoff §3 |
| **Admin (B2B)** | **CONSTRUIR** o login de admin: Entra ID **workforce** + **App Roles** (não só conceito) | handoff §3 |
| **Migração v1→CIAM** | **Passo prático do lab** (executar a migração de `users` v1 → CIAM) | handoff §3 |
| **Gateway** | YARP: proxy, **rate-limit (5/min → 429)**, **output cache (30s)**, CORS, **validação de JWT do CIAM** | handoff §3 |

### Invariante 1 — Identidade do CLIENTE = Entra External ID (CIAM), não workforce

A identidade do **cliente final (fluxo v2)** passa a usar o **Microsoft Entra External ID** (tenant CIAM **separado** do workforce), com:

- **Authority `<tenant>.ciamlogin.com`** (não `login.microsoftonline.com`).
- **1 user flow** self-service de sign-up/sign-in.
- **1 social IdP (Google)** pré-configurado pelo instrutor; **email + OTP** como fallback (handoff §3).
- App Registration tipo **SPA** (Authorization Code + PKCE, sem secret no browser) que o **aluno cria** no tenant CIAM e pluga no MSAL.

O **Azure AD B2C legado está DEPRECIADO** e **não é usado** (handoff §3; memo §3 nota (d): fim de venda 2025-05-01, B2C P2 descontinuado 2026-03-15 — Microsoft direciona para External ID). Ensinar produto morto é antipedagógico.

### Invariante 2 — A única mudança de string no código de identidade é a authority/issuer; o resto do encadeamento é issuer-agnóstico

A migração do caminho ADE-005 (workforce) para o CIAM altera **apenas** `authority`/`issuer`/`audience`:

- **Front (`authV2.ts`):** `authority` muda de `login.microsoftonline.com/<tenant>` → **`<tenant>.ciamlogin.com`** (handoff §3; memo §2.4).
- **Gateway (`Program.cs`):** `AddJwtBearer` aponta o discovery para o **issuer do CIAM** (`<tenant>.ciamlogin.com/.../v2.0/.well-known/openid-configuration`); valida `iss`/`aud`/assinatura do token CIAM (ADE-004 Inv 4 preservada).

> **O encadeamento Gateway → Function → SQL (`oid` → `X-Entra-OID` → `entra_oid`) NÃO muda** (handoff §3; memo §1.3/§5). O gateway extrai o `oid` e propaga `X-Entra-OID` exatamente como na F3 atual; a Function grava `entra_oid` igual. A coluna **não sabe nem se importa** de qual tenant veio o GUID. **Risco técnico de migração: BAIXO.**

### Invariante 3 — `oid` do CIAM continua sendo a chave; há DOIS eixos de `entra_oid` (compra transacional vs cadastro durável)

> **Emendada em 2026-06-25 (v1.1)** após o design de migração da @data-engineer (Dara) — `docs/architecture/migration-v1-ciam-design.md`. A redação original ("nenhuma DDL nova é introduzida por esta ADE"; `entra_oid` só em `purchases`) era verdadeira **apenas para o eixo de provedor de identidade** (o fluxo de compra). Ela **não previa** a distinção entre identidade de COMPRA e identidade de CADASTRO que a migração hands-on (Invariante 6) exige. Esta versão corrige isso de frente (Art. IV — honestidade arquitetural). Ver §"Emenda v1.1 — dois eixos de identidade".

O **`oid`** (Object ID, GUID estável do usuário no tenant CIAM) permanece a chave canônica de identidade do v2. **Sem tabela de mapping** (a ADE-001 segue **aposentada** pela ADE-005; o `oid` é a chave direta — isso não muda com CIAM; memo §5/§7). Mas o `oid` aterrissa em **dois lugares semanticamente distintos**, cada um com seu papel:

| Coluna | Eixo | Semântica | Cardinalidade | Origem do dado | Schema |
|---|---|---|---|---|---|
| **`purchases.entra_oid`** | **Compra (transacional)** | "o `oid` de quem fez ESTA compra v2" — atributo do **evento de compra** | repete por compra (índice **NÃO-unique** filtrado, `phase-03.sql`) | pipeline `oid → X-Entra-OID → entra_oid` (Inv 2) | **inalterado** — já existe desde F3; só muda a origem do GUID (CIAM em vez de workforce) |
| **`users.entra_oid`** | **Cadastro (durável)** | "o `oid` CIAM vinculado a ESTE registro de usuário v1" — atributo da **identidade** | único por usuário (índice **UNIQUE** filtrado) | a **migração de cadastro** hands-on (Inv 6) | **DDL nova aditiva** — `phase-04-ciam-link.sql` (ADD COLUMN + CREATE INDEX, idempotente, ADE-000 Inv 2) |

**Por que dois eixos (e não só `purchases`):** a coluna `purchases.entra_oid` é **transacional** — só é preenchida quando uma compra v2 acontece (verificado em `PurchaseRepository.cs`; índice NÃO-unique em `phase-03.sql:52-61`). Um usuário v1 que **migra o cadastro mas ainda não comprou no v2** não teria onde seu `oid` aterrissar de forma durável em `purchases`. Forçar o vínculo em `purchases` exigiria ou (a) reescrever `entra_oid` em linhas de compra **históricas** por join de email — falsificando o dado (uma compra v1 de maio/2026 **não** foi feita com identidade CIAM), o que viola a honestidade do histórico e o comentário de `phase-01/03` ("linhas v1 históricas não têm identidade Entra"); ou (b) tornar a prova de coexistência (AC-16) dependente de uma compra v2 forçada — frágil em sala. A coluna `users.entra_oid` resolve: vínculo durável, único por usuário (chave natural de match = `users.email`, que é `UQ_users_email`), sem tocar histórico transacional. **Mecanismo, idempotência e rollback completos em `migration-v1-ciam-design.md`** (delegação @data-engineer, matriz de autoridade).

**Schema delta — honestidade do slogan (Art. IV):** o "schema delta zero" vale **literalmente para o eixo de provedor de identidade** (troca workforce→CIAM no fluxo de compra: `purchases.entra_oid` não muda, o pipeline não muda). Para o **lab inteiro**, o delta deixa de ser zero: a migração de cadastro hands-on introduz **uma** migration aditiva/idempotente. Em uma frase: **zero DDL no eixo provedor-de-identidade; uma migration aditiva (`users.entra_oid`) no eixo cadastro.** Ambas respeitam ADE-000 Inv 2 (só `ADD COLUMN`/`CREATE INDEX`, nada destrutivo; bcrypt/`users` intactos).

### Invariante 4 — `entra_oid` (CIAM) COEXISTE com `users` v1 (bcrypt); o contraste v1 vs v2 é o produto didático

A identidade CIAM **não substitui** o caminho v1 homegrown:

- **v1 (homegrown):** `users.id` (int) + `users.password` (bcrypt). **Intacto** — nunca tocado pela migração.
- **v2 (CIAM):** o `oid` do External ID aterrissa em **dois eixos** (Inv 3): `purchases.entra_oid` no fluxo de **compra** (comparação lado-a-lado na mesma `purchases`, ADE-000 Inv 1) e `users.entra_oid` no eixo **cadastro** (mesmo usuário com bcrypt v1 + `oid` CIAM na mesma linha de `users` — a forma mais limpa da coexistência).

Apagar o v1 apagaria a lição central das Quartas: a diferença entre **identidade homegrown** (você gerencia hash/reset/MFA/brute-force) e **identidade gerenciada CIAM** (Microsoft cuida de tudo). **A migração v1→CIAM do lab (Invariante 6) é aditiva — vincula, não destrói** (handoff §3; memo §5).

### Invariante 5 — Admin = workforce + App Roles, CONSTRUÍDO (não só conceito)

O **admin/operador** permanece no **tenant Entra ID workforce** (`login.microsoftonline.com`) com **App Roles** (ex.: `Admin`/`Operator`/`Viewer`). Diferente da ADE-005 (que o tratava como App Registration "que o blueprint já previa"), o owner decidiu **construir** o login de admin como entregável do lab (handoff §3), não deixá-lo no plano conceitual.

Isso materializa o **desenho canônico B2C**: **cliente entra pelo CIAM (`ciamlogin.com`), funcionário pelo workforce (`login.microsoftonline.com`)** — dois mundos de identidade coexistindo (memo §1/§6). O gateway YARP valida os **dois** issuers (Inv 2 é issuer-agnóstica por construção: ADE-004 valida JWT por discovery, então aceitar dois issuers é configuração, não reescrita).

> **A confirmar com owner:** os **nomes exatos das App Roles** (`Admin`/`Operator`/`Viewer` é a convenção herdada da ADE-005 Inv 1; o handoff §3 diz "App Roles" sem enumerar). Marcado como detalhe a confirmar no draft da story — não inventado aqui.

### Invariante 6 — A migração `users` v1 → CIAM é PASSO PRÁTICO do lab (hands-on), e é aditiva

O owner promoveu a migração de **speaker note** (recomendação do memo §5/§7) para **passo executável do lab** (handoff §3). A migração:

- **Vincula** usuários `users` v1 ao tenant CIAM e **liga** o `oid` resultante ao registro existente, gravando-o em **`users.entra_oid`** (eixo cadastro durável, Inv 3) — **não** em `purchases` (que é transacional).
- É **aditiva e idempotente** (ADE-000 Inv 2): **não apaga** `users` nem o bcrypt; não há migração destrutiva. Os dois caminhos permanecem vivos (Inv 4) — a migração demonstra a **convivência**, não a substituição. Idempotência garantida por `UPDATE ... WHERE entra_oid IS NULL` + índice **UNIQUE** filtrado.
- O contraste pós-migração (mesmo usuário, na **mesma linha de `users`**, com `password` bcrypt v1 intacto + `entra_oid` CIAM preenchido) é o ápice didático das Quartas.

> **Mecanismo RESOLVIDO** (delegação @data-engineer cumprida — `docs/architecture/migration-v1-ciam-design.md`, Dara, 2026-06-25): caminho primário = **self-service sign-up no CIAM** com o **mesmo email** do `users` v1 (reusa a competência do bloco cliente CIAM) + **link SQL por email** (`UPDATE users SET entra_oid=@oid WHERE email=@email AND entra_oid IS NULL`); Graph bulk import fica como demonstração opcional. **bcrypt NÃO é importável para o CIAM** — e isso é deliberadamente a lição: o usuário migrado estabelece credencial nova (Google/OTP), o `users.password` bcrypt permanece intacto e válido no caminho v1 (mesmo humano, duas credenciais independentes). Detalhe do passo-a-passo, query de prova e rollback no doc da Dara.

### Invariante 7 — O gateway YARP é o guardião do perímetro e valida o JWT do CIAM (ADE-004 preservada)

Nenhuma mudança no perímetro externo (norte-sul). O Gateway YARP das Quartas entrega, **em código** (ADE-004 Inv 3, paridade de capacidades):

- **Proxy** das rotas v1/v2 para as Functions.
- **Rate-limit 5/min → 429** (`AddRateLimiter`, partição por chave/usuário).
- **Output cache 30s** (`AddOutputCache`).
- **CORS** restrito ao domínio do front.
- **Validação de JWT do CIAM** (`AddJwtBearer` → discovery do issuer `ciamlogin.com`), extração do `oid`, propagação de `X-Entra-OID` downstream.

> **Nota didática (memo §6):** os dois temas das Quartas se encontram exatamente no `AddJwtBearer` do YARP apontando para o discovery do CIAM — *"o gateway é o guardião: ele valida o JWT que o CIAM emitiu antes de deixar passar"*. A prova de que o gateway é **issuer-agnóstico** é validar o token CIAM com a **mesma** mecânica que validava o workforce (só muda a string do discovery). A malha interna leste-oeste (n8n → McpServer) permanece como na ADE-006 Inv 7 — fora do escopo desta ADE.

---

### Invariante 8 — Provisionamento JIT do usuário nato-CIAM: "resolve-or-provision" no primeiro login/compra (adicionada v1.2, insumo/desbloqueio da Story 3.5)

> **Adicionada 2026-06-30 (v1.2).** As Invariantes 3, 4 e 6 assumem um usuário que **já existe** em `users` v1 (Inv 6 "liga o `oid` ao registro **existente**"; Inv 4 "v1: `users.id` + `users.password` bcrypt, **intacto**"). Esta invariante cobre o caso **espelhado e ainda não previsto**: o cliente **nato-CIAM** — cadastrado direto no External ID, que **nunca** teve linha em `users` v1 — que precisa de um registro `users` (com `entra_oid`) para completar uma compra v2, porque `PurchaseRequest.UserId` é `int` obrigatório `[Range(1, int.MaxValue)]` (verificado `src/Fifa2026.V2.Functions/Models/PurchaseRequest.cs:25-27`). Ver §"Emenda v1.2" para a justificativa (por que **emenda**, não "coberto como está" nem ADE nova).

O contrato é **resolve-or-provision**, exposto no endpoint `GET /api/v2/me` (atrás do gateway — ADE-004; confia **só** no `X-Entra-OID` propagado — Inv 7; **nunca** valida o JWT ele mesmo), com **precedência determinística**:

1. **Resolve por `oid`** — `SELECT id FROM dbo.users WHERE entra_oid = @oid`. Se existe (já migrado por Inv 6, ou já JIT-provisionado antes), retorna o `id` **existente**. Nunca cria segundo registro (guarda: `UQ_users_entra_oid`).
2. **Resolve por `email` → LINK (a Inv 6 executada on-demand)** — se **não** há linha por `oid` mas **existe** uma por `email` do claim (um usuário v1 **não-migrado** chegando via CIAM), **vincula** em vez de inserir: `UPDATE dbo.users SET entra_oid=@oid WHERE email=@email AND entra_oid IS NULL` — **exatamente** o UPDATE idempotente da migração de cadastro (`migration-v1-ciam-design.md §4`). Retorna o `id` existente. *(Este arm resolve o caso de borda que o @sm levantou: e-mail CIAM que já existe em `users` — não é um usuário novo, é o mesmo humano; vincula-se, não duplica.)*
3. **Provision (o caso genuinamente novo)** — se não há linha por `oid` **nem** por `email`, **INSERT** de nova linha `users` (nato-CIAM): `entra_oid=@oid`; `name`/`email` dos claims do token CIAM; `role` no `DEFAULT ('user')` (schema, sem ação); `password` = **sentinela fail-closed** (ver Inv 8.1).

**Idempotência sob concorrência (ADE-000 Inv 4; padrão de `PurchaseRepository`):** o WRITE (INSERT do passo 3 ou UPDATE do passo 2) é guardado pelos índices UNIQUE (`UQ_users_entra_oid` **e** `UQ_users_email`) **+** captura de `SqlException` `2627/2601` → **re-resolve** — nunca "SELECT-then-INSERT" como única defesa (evita TOCTOU). Uma corrida entre dois primeiros-logins do mesmo `oid`/`email` colapsa em **uma** linha; o perdedor re-resolve e retorna o `id` vencedor. Espelha `PurchaseRepository.cs:111-119` (o mesmo `2627` de `UQ_purchases_correlation_id`).

**8.1 — `users.password NOT NULL` (o achado da Story 3.5): SENTINELA fail-closed, NÃO nullable.** `users.password` é `NVARCHAR(255) NOT NULL` **sem `DEFAULT`** (verificado `fifa2026-api/database/schema.sql:20`), e o cliente nato-CIAM **não tem bcrypt** (Inv 4: "a Microsoft gerencia a credencial; o bcrypt não viaja"). O INSERT grava um **valor sentinela nomeado, documentado e comprovadamente não-verificável** — um valor que **não é um hash bcrypt válido**, de modo que o caminho de login v1 (`auth.js` / `bcryptjs.compare`) **nunca** autentica esse usuário: **fail-closed por construção** (um nato-CIAM não tem credencial homegrown, e a porta v1 fica fechada para ele pela própria forma do dado). **Rejeitado tornar `password` nullable:** seria `ALTER COLUMN` (não-aditivo — viola ADE-000 Inv 2 "só `ADD COLUMN`/`CREATE INDEX`") e relaxaria uma premissa da qual o v1 homegrown depende (Inv 4 → risco de regressão no caminho v1, que é o **produto didático**). A sentinela **preserva** ADE-000 Inv 2 (**zero DDL** — a única coluna nova, `users.entra_oid`, já existe da Story 2.11) e ADE-007 Inv 4 (v1 intacto); e é a materialização honesta da própria lição da Inv 4 ("com identidade gerenciada você nem tem um hash para guardar"). **O literal exato da sentinela + o teste que prova `bcryptjs.compare(qualquer_entrada, SENTINELA) === false`** (nunca `true`, nunca exceção que deixe o login passar) são **delegados a @data-engineer/@dev** (matriz de autoridade) — a DIREÇÃO (sentinela fail-closed, nomeada, testada) é desta ADE; o valor arbitrário **não** é inventado aqui (Art. IV / AC-4 da Story 3.5).

**8.2 — Fence de esquema (o `/api/v2/me` é `CiamOnly`): o resolve-or-provision só corre para o mundo CIAM, nunca para o admin (adicionado v1.3 — AC da Story 3.5).** O endpoint `/api/v2/me` (resolve-or-provision) DEVE ser autorizado **exclusivamente** para o esquema **CIAM** (cliente), **não** para o **Admin** (workforce). **Por quê — verificado na fonte (`Gateway/Program.cs`):** o transform que injeta `X-Entra-OID` é **GLOBAL** — registrado no topo do pipeline de transforms (`:94-116`), roda para **todos** os clusters e para **qualquer** usuário autenticado, seja pelo esquema `Ciam` ou `Admin` (contraste com o transform de `X-Gateway-Key`, que é **escopado por cluster** via `if ClusterId == backend-v1`, `:118-144`). E a `DefaultPolicy` das rotas v2 aceita **`CiamScheme` OU `AdminScheme`** autenticado (`:393-396`). Logo, **sem o fence**, um **token de admin** (workforce — que também carrega o claim `oid`) satisfaria o `/api/v2/me`, o gateway injetaria o `oid` **do admin** como `X-Entra-OID`, e o resolve-or-provision **criaria/vincularia uma linha de CLIENTE em `users` com a identidade do operador** — corrompendo a base de clientes com o `oid` de um admin (e, pior, o arm de LINK por email poderia atar a linha de um cliente ao operador). O **fence** é uma política `CiamOnly` — o **espelho da `AdminOnly`** já existente (`:408-410`), exigindo o esquema **`Ciam`** em vez do `Admin`: um token workforce roteado pelo selector ao esquema `Admin` **não** satisfaz `CiamOnly` → **401/403** em `/api/v2/me`, e o resolve-or-provision **jamais** corre com um `oid` de admin. *(401 vs 403 é detalhe de implementação — como no raciocínio da própria `AdminOnly` sobre fixar ou não o esquema; ambos fecham o buraco. A **DIREÇÃO** — "admin nunca provisiona cliente" — é desta ADE; a forma exata da policy é de @dev.)* Isto **vira AC da Story 3.5** e soma-se aos flags de segurança abaixo como item **(d)**.

> **Forma validada na implementação (v1.3.1 — gate @architect da Story 3.5, 2026-07-01): o fence é `RequireAssertion` por ISSUER, NÃO scheme-pinning.** A leitura literal "espelho da `AdminOnly` fixando o esquema `Ciam`" (`AddAuthenticationSchemes(Ciam)`) **não fecha o buraco** — e o gate confirmou o porquê na fonte: a rota `/me` herda a `DefaultPolicy` do blanket `MapReverseProxy().RequireAuthorization()` (`Program.cs:595`), e o `AuthorizationMiddleware` **combina** a policy da rota (`CiamOnly`) com a `DefaultPolicy` **UNINDO os `AuthenticationSchemes`** (`AuthorizationPolicy.Combine`); o `AdminScheme` volta pela `DefaultPolicy` e um token workforce autentica por ele, satisfazendo `RequireAuthenticatedUser`. (@dev verificou empiricamente: com scheme-pinning o admin recebeu **200**.) A forma correta é a **mesma estratégia scheme-independent que a `AdminOnly` já usa** — a `AdminOnly` discrimina por `RequireRole` (independe do esquema); a `CiamOnly` discrimina por **`RequireAssertion` sobre o issuer** (`ciamlogin.com` vs `login.microsoftonline.com` — o **mesmo** discriminador, e a mesma constante `CiamIssuerHost`, que o `PolicyScheme` selector já usa: single source of truth). Um token workforce (issuer `login.microsoftonline.com`), mesmo autenticado pelo `AdminScheme` via o merge, **não** satisfaz a assertion → **403 exato** e o backend `/api/v2/me` **nunca é chamado**. Robustez da assertion: checa o claim `iss` e, como fallback, `Claim.Issuer` de qualquer claim (sempre taggeada com o issuer validado pelo handler) — fecha mesmo se o `iss` não for surfaçado como claim. **Empiricamente provado** por `CiamOnlyFenceTests.AdminToken_OnMe_IsRejected_And_Backend_Never_Hit` (403 **exato** + backend nunca chamado) contra o `Program.cs` real (Gateway 31/31). O 403 (não 401) é a prova das **duas** metades do achado: o admin *autenticou* (logo scheme-pinning teria falhado) **e** a assertion de issuer o barrou. A **DIREÇÃO** da ADE ("admin nunca provisiona cliente") é honrada integralmente; esta nota apenas **registra a forma correta** da policy (autoridade delegada a @dev pela AC-3, agora validada pelo gate).

**Claims `name`/`email` — o gateway propaga aditivamente.** Hoje o gateway propaga só `X-Entra-OID` (o `oid`). Como a Function **não** valida o JWT (Inv 7 — só o gateway o faz), ela não pode ler `email`/`name` do token; logo o gateway passa a propagá-los como **headers adicionais** (ex.: `X-Entra-Email`, `X-Entra-Name`). É extensão **aditiva** dos transforms — ADE-004 inalterada no modelo de guardião; Inv 2 ("só muda a string") preservada (é configuração, não reescrita). **A disponibilidade real de `email`/`name` no user flow do tenant CIAM é "a confirmar com owner"** (mesma cautela M-1/M-2 do gate da Story 2.11); fallback documentado se ausente (não assumido).

**Segurança (flag explícito):**
- **(a)** o provisionamento age **exclusivamente** sobre a identidade validada pelo gateway (`X-Entra-OID`); `X-Entra-OID` ausente/inválido → **não** provisiona (jamais cria linha para identidade não-validada — mesmo trust model de `PurchaseEntryFunction`).
- **(b)** o arm de **LINK por email** (passo 2) só é seguro se o email do CIAM for **verificado** (OTP/Google comprovam posse) — senão um cadastro CIAM com o email de uma vítima v1 poderia se vincular à linha dela. **Exigir email verificado no user flow** é "a confirmar com owner".
- **(c)** `name`/`email` são **PII** — **não logar** (mantém a higiene já praticada com o `oid`, AC-12 da Story 2.3).
- **(d)** **Fence de esquema `CiamOnly` (ver 8.2):** `/api/v2/me` autorizado **só** para o esquema **CIAM**, **nunca** Admin. Como o transform `X-Entra-OID` é **global** (`Gateway/Program.cs:94-116`), sem o fence um token de admin faria o gateway injetar o `oid` do operador e o resolve-or-provision criaria uma linha de **cliente** com identidade de **admin**. **Vira AC da Story 3.5.**

**O que NÃO muda (Art. IV — a emenda é aditiva):** toda Inv 1-7 permanece; o encadeamento `oid → X-Entra-OID → entra_oid` segue **issuer-agnóstico**; `purchases.entra_oid` (eixo compra) **intacto**; a coexistência v1/v2 (Inv 4) **intacta**; **nenhuma migration nova** (a sentinela grava na coluna NOT NULL já existente; `users.entra_oid` já existe). A Inv 8 **estende** o modelo de identidade para o cliente nato-CIAM — não reverte nem substitui decisão alguma; por isso é **emenda (v1.2)**, não ADE nova.

---

## O que muda vs ADE-005 (esta ADE a SUPERSEDE)

| Dimensão | ADE-005 (2026-06-03) — superseded | ADE-007 (2026-06-25) — esta |
|---|---|---|
| **Provedor de identidade do CLIENTE** | Tenant **workforce** + App Registration + Easy Auth/MSAL | **Entra External ID (CIAM)** — tenant separado, user flow, social IdP |
| **Authority do cliente** | `login.microsoftonline.com/<tenant>` | **`<tenant>.ciamlogin.com`** |
| **Premissa de atrito do External ID** | "alto e desproporcional" (tenant CIAM + user flows) | **revista:** trial sem subscription/cartão + VS Code → **pré-provisionável** pelo instrutor |
| **Admin** | App Registration + App Roles (conceito previsto) | **Construído** no lab: workforce + App Roles (hands-on) |
| **Migração v1→CIAM** | não tratada (v1 só coexiste) | **Passo prático do lab** (aditivo) |
| **`oid` / `entra_oid` / coluna** | `oid` é a chave; coluna `entra_oid` | **inalterado** — só muda a **origem** do GUID (CIAM) |
| **Encadeamento Gateway→Function→SQL** | issuer-agnóstico | **inalterado** (issuer-agnóstico) |
| **Gateway YARP (ADE-004)** | valida JWT workforce | **preservado** — valida JWT CIAM (e workforce p/ admin) |
| **Coexistência v1 (bcrypt)** | sim | **sim** (mantida — é o produto didático) |

**O que NÃO muda (preservado da ADE-005 e ADEs anteriores):** o `oid` como chave (ADE-001 segue aposentada); a comparação lado-a-lado v1/v2 (ADE-000 Inv 1); o gateway YARP como guardião único do JWT (ADE-004 Inv 4); a propagação `X-Entra-OID` ao McpServer/n8n (ADE-006). **Atenção (v1.1):** o `purchases.entra_oid` continua inalterado, mas o **lab inteiro deixa de ser zero-DDL** — a migração de cadastro adiciona `users.entra_oid` (ver §"Emenda v1.1" abaixo e Inv 3).

---

## Emenda v1.1 — dois eixos de identidade (resposta ao design de migração da @data-engineer)

> **Adicionada 2026-06-25** após @aiox-master (Orion) escalar um conflito de boundary entre esta ADE e o design de migração da @data-engineer (Dara, `docs/architecture/migration-v1-ciam-design.md`). Decisão de @architect (Aria): **ACEITO** a distinção proposta pela Dara. Registro aqui o que mudou e por quê.

**O conflito:** a Invariante 3 (v1.0) afirmava *"nenhuma DDL nova é introduzida por esta ADE"*, com `entra_oid` vivendo só em `purchases`. A Dara, lendo o código real, constatou que **`purchases.entra_oid` é transacional** (gravado por compra v2; índice NÃO-unique em `phase-03.sql:52-61`; `INSERT` em `PurchaseRepository.cs`) — logo **não serve de âncora durável** para migrar o *cadastro* de um usuário v1 que ainda não comprou. Como o owner quer a migração de **cadastro** hands-on (migrar "o usuário João" independente de compra), a Dara adicionou `users.entra_oid` via migration aditiva/idempotente `phase-04-ciam-link.sql`.

**Minha avaliação (verifiquei na fonte da verdade, não por ouvir dizer):** confirmei que `purchases.entra_oid` é transacional (`phase-03.sql`), que `users` não tem `entra_oid` e usa `email` como chave natural única (`UQ_users_email`, `schema.sql:19/27`), e que o **próprio `phase-03.sql:12-17` já deixava a porta aberta**: *"Se @architect preferir `users.entra_oid` no QG, o ajuste é simples e não invalida a story"*. A distinção **identidade de cadastro ≠ identidade de compra** é correta e a minha Inv 3 v1.0 não a previu. A decisão da Dara está **dentro do mandato** que a própria Inv 6 delegou a ela.

**O que muda na ADE-007 (esta emenda):**
1. **Invariante 3 reescrita** — reconhece os **dois eixos**: `purchases.entra_oid` (compra, transacional, inalterado) e `users.entra_oid` (cadastro, durável, DDL nova aditiva). Ver a tabela na Inv 3.
2. **"Schema delta zero" deixa de ser literal para o lab inteiro** — passa a: **zero DDL no eixo provedor-de-identidade (fluxo de compra); uma migration aditiva (`users.entra_oid` + índice UNIQUE filtrado) no eixo cadastro.** Ambas respeitam ADE-000 Inv 2 (só `ADD COLUMN`/`CREATE INDEX`, nada destrutivo).
3. **Invariante 6 atualizada** — o alvo do vínculo é `users.entra_oid` (não `purchases`); mecanismo resolvido (self-service + link SQL por email; bcrypt não-importável vira a lição).

**A narrativa "issuer-agnóstico / só muda a string" AINDA SE SUSTENTA.** A DDL nova é de **outro eixo** e não a contradiz:
- O **eixo provedor-de-identidade** (a tese central das Quartas — "o gateway valida o JWT que o CIAM emitiu; o encadeamento `oid → X-Entra-OID → entra_oid` não muda, só a authority/issuer/aud") permanece **100% issuer-agnóstico e zero-DDL**. `purchases.entra_oid` e o pipeline de compra estão intactos.
- A **DDL nova (`users.entra_oid`)** pertence ao **eixo cadastro/migração** — um tema *diferente e adicional* que o owner trouxe ao escolher a migração hands-on. Ela não toca o issuer, o JWT, o gateway, nem o pipeline de compra. É um **vínculo durável** que a migração de cadastro produz, não uma consequência da troca de provedor.
- Ou seja: **"só muda a string" continua verdadeiro para o código de identidade/gateway**; o que se acrescenta é **"e a migração de cadastro adiciona uma coluna de vínculo"** — duas afirmações verdadeiras sobre dois eixos. A honestidade (Art. IV) é distinguir os eixos, não fundir os dois num slogan único que ficaria falso.

---

## Emenda v1.2 — JIT provisioning do usuário nato-CIAM (Invariante 8; resolve a Task 0 da Story 3.5)

> **Adicionada 2026-06-30** para resolver o **gate de design (Task 0) da Story 3.5** (`docs/stories/3.5.story.md`), que o @sm deixou **BLOQUEADA** aguardando decisão de @architect: *a ADE-007 vigente cobre o JIT de um usuário nato-CIAM, ou precisa de emenda / nova ADE?* Decisão de @architect (Aria): **EMENDA (v1.2 / Inv 8)** — nem "coberto como está", nem ADE nova. Registro aqui o porquê.

**O gap (constatado na fonte, não de ouvido):** as 7 invariantes v1.1 assumem um usuário que **já existe** em `users` v1 — Inv 6 **vincula** (`UPDATE`) um registro **existente**; Inv 4 assume `users.password` (bcrypt) **presente e intacto**. O @sm mapeou as 7 e concluiu (corretamente) que nenhuma cobre o **nato-CIAM**. Verifiquei na fonte: (i) `PurchaseRequest.UserId` é `int` obrigatório `[Range(1, int.MaxValue)]` (`PurchaseRequest.cs:25-27`) — sem linha `users`, não há checkout v2; (ii) `users.password` é `NOT NULL` **sem `DEFAULT`** (`schema.sql:20`) — um INSERT nato-CIAM não tem bcrypt para gravar. O gap é **real e delimitado**.

**Por que EMENDA e não "coberto como está" (rejeito a leitura (a) pura do @sm):** avançar sob "é só a metade que faltava da Inv 6" exigiria reinterpretar **silenciosamente** duas invariantes além do que escrevem (Inv 6 "registro existente"; Inv 4 "bcrypt presente") **e** deixar a decisão do `password NOT NULL` **sem registro** — exatamente a invenção-sem-rastreabilidade que o Art. IV proíbe e que a AC-4 da story guarda. O honesto é tornar a extensão **explícita** (Inv 8).

**Por que EMENDA e não ADE nova (rejeito a leitura (b) na forma "ade-009"):** o JIT **não reverte nem substitui** decisão alguma da ADE-007 — não troca provedor, não toca o encadeamento issuer-agnóstico, não altera a coexistência. Ele **reusa** toda a maquinaria já decidida (o `oid` como chave; o eixo durável `users.entra_oid` — Inv 3; o guarda de idempotência `UQ_users_entra_oid`; o match por email da Inv 6; a propagação `X-Entra-OID` da Inv 7) e **acrescenta** um único caso novo (provisionar em vez de vincular). Isso é a **definição de emenda**, e há **precedente dentro desta própria ADE**: a **v1.1 emendou a Inv 3** (dois eixos) quando o design da @data-engineer revelou um caso não previsto — em vez de abrir ADE nova. Além disso, a **ADE-008 (2026-06-30) já roteou** o "JIT provisioning CIAM (débito #4)" como **"governado por ADE-007"** (ADE-008 §Impacto) — abrir ade-009 contradiz esse roteamento. Convenção observada no repo: **SUPERSEDE (ADE nova) = reversão/remoção** (ADE-005→007, ADE-006→008); **EMENDA (vX.Y) = novo caso na mesma família, sem contradizer** (Inv 3 na v1.1; agora Inv 8 na v1.2). *(A cláusula "Imutável durante EPIC-002 → mudanças via ADE que a supersede" governa **reversões**; emendas **aditivas** que não revertem nada seguem o precedente já aberto pela v1.1.)*

**Síntese honesta das duas leituras do @sm:** a leitura (a) **acerta** que o JIT **reusa** a mesma maquinaria (⇒ não é ADE nova); a leitura (b) **acerta** que "criar uma linha a partir de claims de um token" é um WRITE **categoricamente novo** — nova superfície de segurança **+** o problema do `password` — que nenhuma invariante atual endereça (⇒ não está "coberto como está"). A resposta correta não é (a) **ou** (b): é a **Inv 8**, que **reusa** a maquinaria [de (a)] e **formaliza** o caso novo [de (b)].

**A decisão do `password` (trade-off registrado — Inv 8.1):** **sentinela fail-closed**, não nullable. Nullable seria `ALTER COLUMN` (viola ADE-000 Inv 2, não-aditivo) e relaxaria a premissa que o v1 homegrown assume (Inv 4 → regressão no caminho didático v1); a sentinela é **zero-DDL**, preserva a Inv 4, e é **fail-closed por construção** (login v1 nunca autentica um nato-CIAM). É também a materialização da própria lição da Inv 4.

**Delegação (matriz de autoridade) — o que fica para @data-engineer/@dev; NÃO bloqueia o desbloqueio da story:**
- **@data-engineer:** o **literal exato** da sentinela + confirmar que **nenhuma migration nova** é necessária (a sentinela grava na coluna `NOT NULL` já existente; `users.entra_oid` + `UQ_users_entra_oid` já existem da Story 2.11) — se algo exigir migration, é **aditiva/idempotente** (ADE-000 Inv 2). Confirmar que o `UPDATE` do arm de LINK (passo 2) é o **mesmo** padrão idempotente do script de migração (`migration-v1-ciam-design.md §4`).
- **@dev:** o endpoint `/api/v2/me` (resolve-or-provision, precedência oid→email-link→insert), a captura `2627/2601` + re-resolve, a **config de propagação** dos headers `X-Entra-Email`/`X-Entra-Name` no gateway (aditiva), a chamada do frontend (AC-6), e o **teste** que prova `bcryptjs.compare(entrada, SENTINELA) === false`.
- **A confirmar com owner:** disponibilidade real dos claims `email`/`name` no user flow do tenant CIAM **e** email **verificado** (segurança do arm de LINK — item (b) da Inv 8) — mesma cautela M-1/M-2 do gate da Story 2.11.

---

## Emenda v1.3 — fence de esquema `CiamOnly` na Inv 8 + reafirmação do framing base↔CIAM (Story 3.5 promovida a peça central da Final)

> **Adicionada 2026-07-01 (co-autoria Squad AIOX TFTEC)**, durante a concepção da **Grande Final** (plano `giggly-mapping-hearth.md` §3 "Identidade unificada"). O owner **corrigiu o enquadramento** da unificação de identidade e a Inv 8 ganhou o **fence de segurança** que faltava. É **micro-emenda aditiva** — não reverte nada, segue o precedente das v1.1/v1.2 (adição, não reversão).

**1) O fence (a lacuna de segurança fechada — materializada na Inv 8.2).** A Inv 8 (v1.2) definiu o resolve-or-provision no `/api/v2/me`, mas **não fixou de qual mundo** de identidade ele aceita chamadas. Verifiquei na fonte (`Gateway/Program.cs`) e o gap é **real**: o transform `X-Entra-OID` é **global** (`:94-116`, todos os clusters, qualquer autenticado) e a `DefaultPolicy` v2 aceita **CIAM OU Admin** (`:393-396`). Sem fence, um **token de admin** faria o gateway injetar o `oid` do **operador** e o resolve-or-provision criaria/vincularia uma linha de **cliente** com identidade de admin. O fence — política **`CiamOnly`**, espelho da `AdminOnly` (`:408-410`) — restringe `/api/v2/me` ao esquema **CIAM**. **Detalhe completo na Inv 8.2** (acima) e no flag **(d)**. **É AC da Story 3.5.**

**2) O framing corrigido pelo owner: a unificação é BASE (v1/bcrypt) ↔ CIAM — NÃO CIAM↔workforce.** A "identidade unificada" da Final é trazer os **cadastros já existentes na tabela `users` (v1, bcrypt)** para a identidade gerenciada do **CIAM**, sem big-bang e sem perder ninguém. **NÃO** é unificar o CIAM com o **admin workforce** — esse permanece **token-only / App Role** (Inv 5), **explicitamente FORA do escopo de unificação**. Dois caminhos cobrem todos os usuários da base (reafirmação do que a Inv 3/6/8 já implementam, agora nomeado como o eixo central):
- **Eager** — a **migração em lote das Quartas** (`UPDATE users SET entra_oid ... WHERE email AND entra_oid IS NULL`, Inv 6 / `migration-v1-ciam-design.md`) vincula os usuários da base **conhecidos**.
- **Lazy** — o **`/api/v2/me`** no 1º login via CIAM: resolve por `oid` (já vinculado) → **LINK por email** (Inv 8 passo 2 — *o usuário da base que chega via CIAM: a unificação acontecendo on-demand*) → INSERT (o **nato-CIAM**, genuinamente novo na base — Inv 8 passo 3).

**A coexistência é a lição (reafirmada):** o mesmo humano acaba em **uma linha `users`** — o `password` bcrypt v1 **preservado** (Inv 4) + o `entra_oid` CIAM **vinculado**. bcrypt **não é importável** para o CIAM → a unificação é por **vínculo**, não substituição (mesma linha, duas credenciais independentes). **Admin workforce = token-only, fora deste eixo.**

**3) Story 3.5 promovida de débito P2 a peça central da Final.** No épico da Final (ADE-008 a listava como "débito #4, governado por ADE-007"), a Story 3.5 (`/api/v2/me`, resolve-or-provision + fence) deixa de ser dívida paralela e vira **a Missão "Simplificar/Unificar"** — o unificador base↔CIAM que faz o cliente nato-CIAM (hoje **incapaz de comprar**, pois o checkout exige `users.id` — `PurchaseRequest.cs:25-27`) virar **cidadão de primeira classe** na base, visível ao dashboard admin que já lê `users`. **Não re-drafto a story** (autoridade @sm/@po) — esta emenda **aponta** a promoção e adiciona o fence como AC.

**Por que emenda (não ADE nova):** idêntico ao racional da v1.2 — o fence e o framing **reusam** toda a maquinaria da Inv 8 e **não revertem** decisão alguma (o admin já era workforce/token-only; o `/api/v2/me` já confiava só no `X-Entra-OID`). É **precisão de segurança + nomeação de eixo**, não nova decisão de provedor. Segue o precedente v1.1/v1.2.

---

## Rationale

### Por que reabrir o External ID que a ADE-005 havia descartado?

- **A premissa que matou o CIAM mudou.** A ADE-005 adiou por atrito (tenant CIAM + user flows ao vivo). Hoje há **trial sem subscription/cartão** + VS Code + get-started guide — o instrutor **pré-provisiona** o tenant/user flow/social IdP, e o aluno só cria a App Reg SPA e pluga a authority (memo §4). O atrito caiu para **dentro do orçamento de 6h** quando pré-provisionado.
- **Fidelidade B2C real.** External ID **é** o produto CIAM da Microsoft (sucessor do Azure AD B2C). Ensinar o cliente final num tenant workforce é semanticamente errado e a própria ADE-005 admitiu isso (memo §0).
- **Sucessor oficial.** B2C legado está em depreciação (memo §3); ensinar o produto vivo é a escolha pedagógica correta.

### Por que admin no workforce e cliente no CIAM (dois mundos)?

- É o **desenho canônico** de produto B2C: cliente externo no CIAM, funcionário interno no workforce (memo §1/§6). Construir os dois torna o lab fiel à realidade e ensina a distinção `ciamlogin.com` vs `login.microsoftonline.com`.

### Por que coexistência v1/v2 (e migração aditiva, não destrutiva)?

- O **contraste homegrown vs gerenciado é a lição** (memo §5). A migração aditiva (Inv 6) demonstra a convivência sem destruir o caminho v1 — preserva ADE-000 Inv 1 e Inv 2.

### Por que o risco técnico é baixo apesar de trocar o provedor?

- Porque o encadeamento é **issuer-agnóstico** (Inv 2): só authority/issuer/aud mudam de string; `oid → X-Entra-OID → entra_oid` é idêntico (memo §1.3). O gateway YARP (ADE-004) já valida JWT por discovery — apontar para outro issuer é configuração.

---

## Consequences

### Positivas

- ✅ **Fidelidade B2C real:** cliente no produto CIAM correto (External ID), admin no workforce — desenho canônico, didaticamente honesto.
- ✅ **Sucessor oficial** do B2C legado (depreciado) — ensina o produto vivo.
- ✅ **Custo US$0** (free 50K MAU; trial sem cartão) — irrelevante o uso de lab vs o limite (memo §2.5).
- ✅ **Risco técnico baixo:** encadeamento issuer-agnóstico intacto; só authority/aud mudam (Inv 2).
- ✅ **ADE-004 preservada:** gateway YARP segue como guardião único do JWT; valida CIAM com a mesma mecânica.
- ✅ **Coexistência v1/v2 mantida:** o contraste didático sobrevive; migração aditiva o reforça.
- ✅ **Resolve o débito semântico** que a ADE-005 registrou honestamente (workforce ≠ CIAM para cliente).

### Negativas / Trade-offs aceitos

- ⚠️ **Dois mundos de identidade num único lab (CIAM cliente + workforce admin):** mais carga cognitiva. Mitigado: **slide de desambiguação obrigatório** (Connect vs Entra ID vs External ID, memo §1) na abertura de F2; e o gateway issuer-agnóstico mostra que a mecânica é a mesma.
- ⚠️ **Risco de TEMPO — provavelmente estoura 6h** (handoff §4): Gateway + cliente CIAM + **admin construído** + **migração hands-on** é um escopo grande. **Esta é a decisão A/B pendente do owner** — ver §Dimensionamento & Recomendação A/B.
- ⚠️ **Trial CIAM expira em 30 dias** (memo §2.6): impacto operacional entre turmas. Mitigado: recriar por turma via script/VS Code versionado; ou converter para tenant com subscription (free 50K MAU) se o lab for recorrente.
- ⚠️ **Social IdP exige app no Google (client id/secret):** dependência externa. Mitigado: **instrutor pré-configura** o Google IdP no tenant; **email + OTP** como fallback de zero dependência (handoff §3).
- ⚠️ **Migração como passo de lab consome tempo** (vs speaker note que o memo §7 recomendava): o owner aceitou o custo pelo valor hands-on. Mitigado pela divisão A/B (abaixo).
- ⚠️ **Authority errada (workforce vs ciamlogin) no MSAL** é o erro clássico: Mitigado por checklist explícito `*.ciamlogin.com` ≠ `login.microsoftonline.com` no PORTAL-GUIDE (memo §7 riscos).

---

## Alternatives Considered (rejeitadas)

### Alt 1 — Manter a ADE-005 (workforce + Easy Auth/MSAL como identidade do cliente)

- **Rejeitada porque:** semanticamente errada para o cliente final (workforce modela funcionário/B2B, não consumidor). A premissa de atrito que a justificava **caiu** (trial sem subscription + VS Code). O owner decidiu adotar o produto correto (External ID) nas Quartas (handoff §3). A ADE-005 fica **superseded** por esta. *(O workforce não é descartado — migra para o papel correto: o **admin**, Inv 5.)*

### Alt 2 — Azure AD B2C legado (o CIAM antigo)

- **Rejeitada porque:** **em depreciação** — fim de venda 2025-05-01, B2C P2 descontinuado 2026-03-15; Microsoft direciona para External ID (memo §3 nota (d)). Ensinar produto morto é antipedagógico. Explicitamente vetado pelo owner (handoff §3: "B2C legado depreciado — não usar").

### Alt 3 — 3rd-party CIAM (Auth0 / Cognito)

- **Rejeitada porque:** o workshop é "Copa do Mundo **Azure**"; sair do Azure quebra a narrativa e adiciona vendor externo (memo §3 (e)). External ID entrega CIAM real dentro do Azure.

### Alt 4 — Migração v1→CIAM como speaker note (recomendação original do memo §7)

- **Rejeitada pelo owner** em favor de **passo prático hands-on** (handoff §3). O memo recomendava speaker note para caber em 6h; o owner priorizou o valor didático da migração executada. Consequência: pressão de tempo → tratada na §Dimensionamento & Recomendação A/B.

### Alt 5 — Substituir o v1 (bcrypt) pelo CIAM (em vez de coexistir)

- **Rejeitada porque:** destruiria o contraste homegrown vs gerenciado, que é o produto pedagógico (memo §5). Quebraria ADE-000 Inv 1 (comparação lado-a-lado na mesma `purchases`). A migração do lab é **aditiva** (Inv 6), não substitutiva.

---

## Validation

Esta substituição é considerada **validada** quando:

- [ ] Tenant External ID (trial) provisionado pelo instrutor com **1 user flow** sign-up/sign-in + **Google IdP** (email+OTP fallback) — fora do relógio da aula.
- [ ] Aluno cria **App Registration tipo SPA** no tenant CIAM (redirect URI localhost + URL prod).
- [ ] `authV2.ts` usa **`authority = <tenant>.ciamlogin.com`** (não `login.microsoftonline.com`) — checklist de authority confirmado.
- [ ] Login CIAM (sign-up self-service + social Google) funciona no SPA via MSAL — aluno demonstra fluxo no browser.
- [ ] Gateway YARP (`Program.cs`) valida o JWT do **issuer CIAM** (`AddJwtBearer` → discovery `ciamlogin.com`), extrai `oid`, propaga `X-Entra-OID` (ADE-004 Inv 4 preservada).
- [ ] Function grava `purchases.entra_oid` (origem CIAM) **ao lado** do registro v1 — **eixo compra sem mudança de schema** (zero-DDL; ADE-000 Inv 2).
- [ ] **Migration `phase-04-ciam-link.sql` aplicada** — `users.entra_oid` (UNIQUEIDENTIFIER NULL) + índice UNIQUE filtrado, aditiva/idempotente (**eixo cadastro**, Inv 3).
- [ ] Rate-limit retorna **429** além de 5/min; **output cache 30s** confirmado; CORS restrito ao front (handoff §3).
- [ ] **Admin** (workforce + App Roles) construído e funcional — login de admin separado do cliente.
- [ ] **Migração `users` v1 → CIAM** executada como passo do lab: self-service sign-up (mesmo email) + `UPDATE users.entra_oid ... WHERE entra_oid IS NULL` (idempotente); **aditiva** (v1/bcrypt intactos); prova de coexistência na mesma linha de `users` (Inv 6 / `migration-v1-ciam-design.md`).
- [ ] B2C legado **não** provisionado em momento algum.

---

## Impact on EPIC-002

### Stories que precisam de re-draft / criação

| Story | Impacto | Ação (executor) |
|---|---|---|
| **2.3 (F3)** | A F3 atual (workforce) é **re-narrada como "admin"** (Inv 5); a identidade do **cliente** migra para o CIAM (Inv 1). AC de authority/issuer e PORTAL-GUIDE mudam para `ciamlogin.com`. | **@sm** re-drafta (esta ADE alimenta o draft; @architect satisfeito). |
| **Nova story (Quartas)** | Materializa Gateway YARP + cliente CIAM + (admin + migração). **Pode virar 1 ou 2 stories** conforme a decisão A/B abaixo. | **@sm** drafta após o owner bater o martelo A/B. |

> **NÃO re-drafto stories** (autoridade de @sm). Esta ADE aponta o impacto e fecha a decisão arquitetural.

### Artefatos a atualizar (apontados para os owners)

- **Blueprint (`docs/workshops/2026-blueprint-living-lab-azure.md`, seções 3/8/F3):** reverter a substituição que a ADE-005 fez — camada cliente volta a **External ID/CIAM**; camada admin = workforce + App Roles; stack table e Identity Strategy reescritas — **@pm**.
- **EPIC-002 (`docs/epics/EPIC-002-*`):** stack, **Risco #2** (atrito External ID — agora **mitigado** por trial sem subscription, não eliminado), **Risco #6** (revisitar com a pressão de tempo A/B) — **@pm**.
- **ADE-005:** anotar como **superseded por ADE-007** (não editar o conteúdo histórico; o status de superseção é registrado aqui e no header desta ADE) — **@architect** (registrado).
- **ADE-000 "Related" / ADE-004 / ADE-006:** referências a "identidade ADE-005" passam a apontar **ADE-007** onde o tópico é o provedor de identidade do cliente — **@architect** (registrado; conteúdo dessas ADEs não muda — `oid`/`X-Entra-OID`/encadeamento intactos).
- **Diagrama draw.io das Quartas** (Gateway + dois mundos de identidade) a desenhar quando o build acontecer — **@ux/@architect** (handoff §5).

---

## Dimensionamento & Recomendação A/B

> **Esta seção responde à decisão pendente do owner** (handoff §4): dividir as Quartas em 2 sessões (Quartas-A = Gateway + cliente CIAM; Quartas-B = admin workforce+roles + migração v1→CIAM) **ou** sessão única?
>
> **🟢 DECISÃO DO OWNER (2026-06-25):** **SESSÃO ÚNICA completa**, com a **migração mantida hands-on** (Inv 6). O owner aceitou conscientemente o estouro de tempo (~7,5–9,5h) em favor de preservar todas as decisões hands-on e a continuidade narrativa. A recomendação da Aria (dividir A/B) **não foi adotada**; fica registrada abaixo como análise. Consequência operacional: o roteiro da aula deve sinalizar a duração estendida (lab "longo") e prever pontos de pausa naturais (ao fim do bloco 2, cliente CIAM) caso a turma precise quebrar em 2 encontros na prática. **1 story única** (não A/B) — orienta o draft do @sm.

### Estimativa de esforço por bloco de trabalho (horas de aula ao vivo)

> Premissas: instrutor **pré-provisiona** o tenant CIAM + user flow + Google IdP fora do relógio (memo §4) e o gateway YARP já existe em código no repo (ADE-004, story 2.2 Done — a aula **demonstra e configura**, não escreve do zero). Estimativas em **horas de aula** (demonstração + aluno replicando no Portal/código), confiança MÉDIA (memo §0 marca a estimativa de tempo como dependente de pré-provisionamento).

| # | Bloco | Atividade | Estimativa | Observação |
|---|---|---|---|---|
| **1** | **Gateway YARP (plano de dados)** | Subir/configurar YARP na frente das Functions; rate-limit 5/min→429, output cache 30s, CORS; ver o proxy roteando | **1,5–2,0 h** | Código já existe (ADE-004); aula é deploy + config + demonstrar as políticas no edge |
| **2** | **Cliente External ID / CIAM** | Aluno cria App Reg SPA no tenant CIAM; pluga `authority=ciamlogin.com` no MSAL; demonstra sign-up self-service + login Google no browser; YARP valida o JWT do CIAM e propaga `X-Entra-OID`; vê `entra_oid` no SQL | **2,0–2,5 h** | Inclui o "encontro" Gateway×Identidade (AddJwtBearer→discovery CIAM). Pré-provisionamento do instrutor é o que segura isso em ~2h |
| **3** | **Admin workforce + App Roles** | Construir App Registration no workforce; definir App Roles; login de admin separado; gateway aceita o 2º issuer | **1,5–2,0 h** | Segundo mundo de identidade — novo tenant/authority na cabeça do aluno; exige o slide de desambiguação |
| **4** | **Migração `users` v1 → CIAM** | Executar import/link de `users` v1 → CIAM; vincular `entra_oid`; provar coexistência (mesmo usuário: bcrypt v1 + `entra_oid` CIAM) | **1,5–2,0 h** | Hands-on (decisão do owner). Mecanismo a confirmar (Inv 6) — Graph import/link tem cerimônia; é o bloco de maior incerteza de tempo |
| | **Buffer** (perguntas, troubleshooting de authority errada, OTP/Google, CORS) | | **~0,5–1,0 h** | Erros de authority `ciamlogin` vs `microsoftonline` e config de IdP social são os clássicos |
| | **TOTAL (tudo numa sessão)** | | **≈ 7,5–9,5 h** | **Estoura as ~6h** (handoff §7: padrão dos labs ~não passar muito de 6h) |

### Recomendação fundamentada

**RECOMENDO DIVIDIR em duas sessões (A/B).** Justificativa:

1. **Aritmética de tempo:** o conjunto soma **~7,5–9,5 h** de aula — bem acima do padrão de **~6h/lab** do workshop (handoff §7). Mesmo com pré-provisionamento agressivo, os quatro blocos não cabem com qualidade numa sessão; o risco é cortar a migração (o bloco mais didático e o que o owner fez questão de tornar hands-on) por falta de tempo.
2. **Carga cognitiva — dois mundos de identidade:** introduzir CIAM (cliente) **e** workforce (admin) **e** uma migração entre paradigmas no mesmo fôlego sobrecarrega o aluno. A divisão dá tempo de **assentar o conceito CIAM** antes de adicionar o segundo mundo (workforce) e a migração.
3. **Autossuficiência narrativa de cada metade** (cada sessão entrega valor fechado):
   - **Quartas-A — "O gateway e o cliente" (≈ 4,0–5,0 h):** Gateway YARP (bloco 1) + cliente External ID/CIAM (bloco 2) + buffer. **Entrega autossuficiente:** o aluno sai com o gateway validando o JWT de um **cliente real CIAM** (sign-up self-service + login Google), `entra_oid` gravado no SQL ao lado do v1. A narrativa "o gateway é o guardião que valida o JWT que o CIAM emitiu" fecha aqui — é uma aula completa por si só.
   - **Quartas-B — "Os dois mundos e a migração" (≈ 3,5–5,0 h):** admin workforce + App Roles (bloco 3) + migração `users` v1 → CIAM (bloco 4) + buffer. **Entrega autossuficiente:** sobre a base da A (cliente CIAM já funcionando), adiciona o **admin no workforce** (desenho canônico B2C: cliente externo + funcionário interno) e culmina na **migração hands-on** que prova a coexistência v1/v2 — o ápice didático. Depende da A como pré-requisito narrativo, mas é uma aula fechada com seu próprio clímax.

### Corte exato recomendado

> **Linha de corte: ao fim do bloco 2 (cliente CIAM validado no gateway, `entra_oid` no SQL).**

| Sessão | Blocos | Entrega autossuficiente | Estimativa |
|---|---|---|---|
| **Quartas-A** | 1 (Gateway YARP) + 2 (cliente CIAM) | Gateway com rate-limit/cache/CORS **+ cliente External ID real** (sign-up + Google) validado pelo YARP; `entra_oid` CIAM gravado ao lado do v1 | **≈ 4,0–5,0 h** |
| **Quartas-B** | 3 (admin workforce + App Roles) + 4 (migração v1→CIAM) | **Dois mundos** (cliente CIAM + admin workforce) + **migração hands-on** provando coexistência v1/v2 | **≈ 3,5–5,0 h** |

**Por que o corte é exatamente aí:** o bloco 2 fecha o "encontro Gateway × Identidade" (o `AddJwtBearer` apontando para o CIAM) — é o **clímax técnico** que torna a A uma aula completa. O segundo mundo (admin/workforce) e a migração são naturalmente uma **camada por cima** de um cliente CIAM já funcionando, o que dá à B um ponto de partida limpo e seu próprio arco. Cortar antes (ex.: separar só o Gateway) deixaria a A sem identidade — anticlímax; cortar depois (admin junto da A) reempurra para >6h.

> **Se o owner preferir sessão única** (apesar da recomendação): o caminho de menor risco é **rebaixar a migração (bloco 4) de hands-on para demonstração guiada/speaker note** (recomendação original do memo §7), recuperando ~1–1,5 h e trazendo o total para a fronteira das 6h. Mas isso **reverte uma decisão explícita do owner** (handoff §3: migração hands-on) — por isso a recomendação primária é **dividir A/B**, preservando todas as decisões do owner.

---

**Authority:** Aria (Architect) — designada por @aiox-master para decisões de seleção de tecnologia, identidade e integração. Detalhe do mecanismo de migração (Graph import/link) e otimização de query são delegáveis a **@data-engineer** (matriz de autoridade).
**Review cycle:** Imutável durante EPIC-002. Mudanças → nova ADE que a supersede.

## Change Log

| Date | Author | Description |
|---|---|---|
| 2026-07-01 | @architect (Aria) | **v1.3.1 — micro-emenda (gate da Story 3.5): registrada a FORMA VALIDADA do fence `CiamOnly` — `RequireAssertion` por ISSUER, não scheme-pinning.** No quality gate da Story 3.5 confirmei na fonte (`Gateway/Program.cs`, `dotnet test` 31/31) o achado do @dev: fixar o esquema (`AddAuthenticationSchemes(Ciam)`) NÃO fecha o buraco, porque o merge da policy da rota com a `DefaultPolicy` do blanket `MapReverseProxy().RequireAuthorization()` UNE os `AuthenticationSchemes` (o `AdminScheme` volta e o workforce autentica — @dev viu 200 com scheme-pinning). A forma correta é scheme-independent, idêntica em estratégia à `AdminOnly` (que usa `RequireRole`): `CiamOnly` usa `RequireAssertion` sobre o issuer (`ciamlogin.com`, a mesma constante `CiamIssuerHost` do selector — single source of truth). Empiricamente provado por `AdminToken_OnMe_IsRejected_And_Backend_Never_Hit` (403 exato + backend nunca chamado; o 403 prova as duas metades — o admin autenticou pelo merge E a assertion o barrou). **Aditiva** (não reverte a v1.3 — precisa a "forma exata da policy" que a Inv 8.2 já delegava a @dev). Detalhe na sub-seção 8.2 (nota "Forma validada na implementação"). Gate: **PASS**. |
| 2026-07-01 | @architect (Aria) · Squad AIOX TFTEC | **v1.3 — Fence de esquema `CiamOnly` na Inv 8 (nova sub-seção 8.2 + flag (d)) + reafirmação do framing base↔CIAM; Story 3.5 promovida a peça central da Final.** Durante a concepção da Grande Final (plano `giggly-mapping-hearth.md` §3), fechei a **lacuna de segurança** da Inv 8: o `/api/v2/me` (resolve-or-provision) DEVE ser `CiamOnly`. **Verificado na fonte (`Gateway/Program.cs`):** o transform `X-Entra-OID` é **global** (`:94-116`, todos os clusters/qualquer autenticado — contraste com o `X-Gateway-Key` escopado por cluster em `:118-144`) e a `DefaultPolicy` v2 aceita **CIAM OU Admin** (`:393-396`); **sem o fence**, um token de **admin** faria o gateway injetar o `oid` do operador e o resolve-or-provision criaria uma linha de **cliente** com identidade de **admin**. Fence = política `CiamOnly`, espelho da `AdminOnly` (`:408-410`). **Framing corrigido pelo owner:** a unificação é **BASE v1 (bcrypt) ↔ CIAM** (eager = migração em lote das Quartas + lazy = `/api/v2/me` link-por-email), **NÃO** CIAM↔workforce — o admin permanece **token-only, FORA do escopo de unificação**. Mesma linha `users` (bcrypt preservado + `entra_oid` vinculado); bcrypt não-importável → vínculo, não substituição. **Story 3.5 sobe de débito P2 a peça central** (Missão Simplificar/Unificar). **Aditiva** (não reverte nada — precedente v1.1/v1.2): o admin já era workforce/token-only, o `/api/v2/me` já confiava só no `X-Entra-OID`. **Não re-drafto a story** (autoridade @sm/@po) — aponto a promoção + o fence como AC. Co-autoria Squad AIOX TFTEC. |
| 2026-06-30 | @architect (Aria) | **v1.2 — Invariante 8 adicionada (JIT provisioning do usuário nato-CIAM), resolvendo a Task 0 (gate de design) da Story 3.5.** Decisão: **EMENDA** — nem "coberto como está", nem ADE nova (ade-009). As Inv 3/4/6 assumem um usuário v1 **existente**; a Inv 8 cobre o **nato-CIAM** (cadastro direto no External ID, sem linha v1) via contrato **resolve-or-provision** no `/api/v2/me`: resolve por `oid` → **LINK** por email (Inv 6 on-demand, cobre o caso de borda de e-mail já existente) → **INSERT** (o caso novo), idempotente pelos índices UNIQUE (`UQ_users_entra_oid`+`UQ_users_email`) + captura `2627/2601` (padrão `PurchaseRepository`, ADE-000 Inv 4). **`users.password NOT NULL` sem `DEFAULT` (achado da Story 3.5, `schema.sql:20`) resolvido por SENTINELA fail-closed** (não-verificável, nomeada, testada) — **rejeitado nullable** (`ALTER COLUMN` viola ADE-000 Inv 2 e a premissa v1 da Inv 4). Gateway propaga `email`/`name` como headers **aditivos** (`X-Entra-Email`/`X-Entra-Name`; ADE-004 inalterada). **Delegado:** literal da sentinela + teste `bcryptjs` + "sem migration nova" → @data-engineer/@dev; claims disponíveis + email verificado no tenant → "a confirmar com owner". **Aditiva:** Inv 1-7 e o encadeamento issuer-agnóstico preservados; nenhuma decisão revertida (por isso emenda, não supersessão — precedente da v1.1). Tudo verificado na fonte (`PurchaseRequest.cs`, `schema.sql`, `phase-04-ciam-link.sql`, `PurchaseRepository.cs`, `PurchaseEntryFunction.cs`, `migration-v1-ciam-design.md`). |
| 2026-06-25 | @architect (Aria) | **v1.1 — Inv 3 emendada (dois eixos de identidade), aceitando o design de migração da @data-engineer** (`migration-v1-ciam-design.md`). Após verificar na fonte da verdade que `purchases.entra_oid` é **transacional** (`phase-03.sql`, índice NÃO-unique; `PurchaseRepository.cs`) e que `phase-03.sql:12-17` já deixava a porta aberta para `users.entra_oid`, **ACEITO** adicionar `users.entra_oid` (migration aditiva/idempotente `phase-04-ciam-link.sql`, índice UNIQUE filtrado) como **eixo cadastro durável**, distinto do `purchases.entra_oid` transacional (**eixo compra**). (1) Inv 3 reescrita com a tabela dos dois eixos. (2) **"Schema delta zero" deixa de ser literal para o lab inteiro** → "zero DDL no eixo provedor-de-identidade; uma migration aditiva no eixo cadastro" (ADE-000 Inv 2 respeitada). (3) Inv 4 e Inv 6 atualizadas (alvo do vínculo = `users.entra_oid`; mecanismo resolvido = self-service + link SQL por email; bcrypt não-importável vira a lição). (4) Nova §"Emenda v1.1 — dois eixos de identidade". (5) Validation/Related/header atualizados. **A narrativa issuer-agnóstico ("só muda a string") permanece verdadeira** — a DDL nova é de outro eixo (cadastro/migração) e não toca issuer/JWT/gateway/pipeline de compra. |
| 2026-06-25 | @aiox-master (Orion) | **Decisão A/B fechada pelo owner: SESSÃO ÚNICA completa** com migração hands-on preservada (estouro ~7,5–9,5h aceito). Recomendação de dividir A/B registrada como análise, não adotada. Quartas = **1 story única**. |
| 2026-06-25 | @architect (Aria) | ADE-007 criada — **supersede ADE-005**. Identidade do **cliente** migra para **Entra External ID (CIAM)** (`ciamlogin.com`, user flow, Google IdP + OTP fallback, trial 30d); **workforce reposicionado para o admin** (App Roles, construído hands-on); **migração `users` v1 → CIAM** como passo prático aditivo; gateway YARP valida JWT do CIAM (ADE-004 preservada, issuer-agnóstica). 7 invariantes; encadeamento `oid → X-Entra-OID → entra_oid` e coexistência v1/v2 **inalterados**. Inclui §Dimensionamento & Recomendação A/B (estimativa ≈7,5–9,5h sessão única → **recomenda dividir** em Quartas-A [Gateway+cliente CIAM, ≈4–5h] e Quartas-B [admin workforce+roles + migração, ≈3,5–5h], corte ao fim do bloco cliente CIAM). Itens sem fonte no handoff/memo marcados "a confirmar com owner". |

-- =====================================================
-- Migration: phase-08-contained-users.sql — contained users (Managed Identity) + RBAC menor-privilégio no Azure SQL
-- Story: 4.1 — Blindar (data-plane): Managed Identity + Key Vault (EPIC-004 "Nível Produção", Missão Blindar)
-- ADE-009 Invariante 2  → data-plane via MI+Entra; menor-privilégio por serviço (DDL delegada a @data-engineer)
-- ADE-008 Invariante 1  → regra de ouro "o chatbot/McpServer NUNCA escreve" — aqui vira GARANTIA DE RBAC
-- ADE-000 Invariante 2  → schema aditivo/idempotente (NUNCA destrutivo)
-- Autor: Dara (@data-engineer) · Squad AIOX TFTEC · 2026-07-01
-- =====================================================
-- O QUE ESTE SCRIPT FAZ
--   Cria um "contained user" (usuário de banco mapeado a uma Managed Identity do Entra, SEM SENHA)
--   para cada serviço que autentica no Azure SQL via MI-AAD
--   (Authentication=Active Directory Managed Identity), e concede a CADA um SOMENTE os database
--   roles de que precisa (menor-privilégio — ADE-009 Inv 2, INVARIANTE e não sugestão).
--   Substitui o modelo antigo (uma connection string SQL-auth adminLogin/adminPassword compartilhada)
--   por identidades NOMEADAS, sem segredo, com privilégio mínimo por serviço.
--
-- CONTRATO DE RBAC (serviço → papel) — entregue pelo @dev na Story 4.1 (AC-7), verificado na fonte:
--   +-----------------------------+-----------------------------------+-------------------------------+
--   | Serviço (Managed Identity)  | Acesso real ao SQL (verificado)   | Database role(s)              |
--   +-----------------------------+-----------------------------------+-------------------------------+
--   | Functions (F1) app          | INSERT purchases (Consumer) +     | db_datawriter + db_datareader |
--   |  = Entry + Status + Consumer | SELECT (Status; INSERT..SELECT)   |                               |
--   | McpServer                   | SÓ SELECT (chatbot read-only)     | db_datareader   E NADA MAIS   |
--   | Backend v1 (Node/Express)   | CRUD completo (auth/tickets/...)  | db_datawriter + db_datareader |
--   | Gateway (YARP)              | NÃO acessa SQL (perímetro)        | (nenhum — sem contained user) |
--   +-----------------------------+-----------------------------------+-------------------------------+
--
--   Por que a Functions app recebe writer+reader (e não "Entry/Status=reader, Consumer=writer"):
--   um contained user mapeia UMA Managed Identity, e uma MI é POR APP. Entry, Status e Consumer estão
--   no MESMO Function App (src/Fifa2026.V2.Functions/Functions/*.cs — verificado) → UMA MI, UM contained
--   user. Como o Consumer (co-hospedado) GRAVA (PurchaseRepository.InsertPurchaseAsync) e o Status /
--   INSERT..SELECT LÊ (ticket_categories), o papel do app é a UNIÃO: db_datawriter + db_datareader.
--   (Se — e SOMENTE se — @devops separar Consumer e Entry/Status em Function Apps distintos com MIs
--   distintas, aí sim: Entry/Status-MI = db_datareader; Consumer-MI = db_datawriter + db_datareader.)
--
--   McpServer = db_datareader E NADA MAIS (regra de ouro ADE-008 Inv 1): FifaQueryRepository só faz
--   SELECT (cabeçalho do arquivo, linha 15: "Acesso SOMENTE leitura — o McpServer nunca grava"). Aqui
--   isso deixa de ser só uma propriedade do CÓDIGO e vira GARANTIA DO BANCO: mesmo que o código
--   regredisse e tentasse um INSERT, o contained user read-only o BLOQUEIA (defense-in-depth — duas
--   travas independentes: código + RBAC).
--
-- -----------------------------------------------------------------------------------------------------
-- IMPORTANTE — PLACEHOLDERS — @devops SUBSTITUI antes de rodar (NÃO invento nomes de MI nem GUIDs — Art. IV)
--     Troque cada token <mi-*> pelo NOME REAL da Managed Identity no Entra:
--        <mi-functions-app>  → a MI do Function App F1 (Entry + Status + Consumer)
--        <mi-mcpserver>      → a MI do McpServer (Container App)
--        <mi-backend-v1>     → a MI do backend v1 (fifa2026-api, App Service)
--     Convenção (recomendação @dev, decisão final @devops): System-Assigned por serviço → o NOME do
--     contained user é o NOME DO RECURSO Azure do serviço (Function App / Container App / Web App).
--     Se User-Assigned → é o nome do recurso da própria Managed Identity.
--     Não-substituído: o CREATE USER FALHA (principal inexistente no Entra) — fail-safe, não cria
--     usuário-lixo silenciosamente.
-- -----------------------------------------------------------------------------------------------------
-- IMPORTANTE — PRÉ-REQUISITOS (sem eles, CREATE USER ... FROM EXTERNAL PROVIDER FALHA):
--     (1) Azure AD admin CONFIGURADO no SQL Server (recurso Microsoft.Sql/servers/administrators) — o
--         param condicional já existe em infra/modules/sql-database.bicep (Story 4.1, AC-4). @devops
--         passa o objectId/login do admin ao módulo (ou configura via Portal/CLI) ANTES deste script.
--     (2) EXECUTAR este script CONECTADO COMO esse Azure AD admin, via autenticação AAD (ex.:
--         `sqlcmd -G`, SSMS "Microsoft Entra MFA", Azure Data Studio AAD) — NÃO via SQL auth (adminLogin).
--         Motivo: criar usuário FROM EXTERNAL PROVIDER exige um token AAD para o SQL resolver o nome da
--         MI no Microsoft Graph; uma sessão SQL-auth não consegue (falha "Principal ... could not be found").
--     (3) As Managed Identities já PROVISIONADAS no Entra (@devops) — a resolução do nome é contra o Graph.
--     (4) Rodar no BANCO DA APLICAÇÃO (FIFA2026Tickets), NÃO em master — contained users FROM EXTERNAL
--         PROVIDER moram no user database (mesma disciplina do schema.sql, linha 8). A trava abaixo aborta
--         se detectar master.
--
-- IDEMPOTENTE (ADE-009 §Delegação exige o padrão de phase-04-ciam-link.sql): cada CREATE USER e cada
--   ALTER ROLE é guardado por IF NOT EXISTS → rodar 2x NÃO duplica usuário nem membership, NÃO erra.
-- ADITIVO APENAS (ADE-000 Inv 2): só CREATE USER + ALTER ROLE ADD MEMBER. NENHUM DROP USER, NENHUM
--   ALTER ROLE DROP MEMBER, NENHUM REVOKE. Rollback (se necessário) é script dedicado e separado —
--   nunca embutido/destrutivo aqui.
--
-- ANTI-HALLUCINATION (Art. IV): CREATE USER ... FROM EXTERNAL PROVIDER, ALTER ROLE ... ADD MEMBER,
--   db_datareader / db_datawriter (fixed database roles), sys.database_principals (type 'E' =
--   EXTERNAL_USER), sys.database_role_members — todos T-SQL/Azure SQL REAIS e documentados pela
--   Microsoft. Nomes de MI e GUIDs NÃO inventados (placeholders acima). Serviços que tocam SQL
--   confirmados por grep: `SqlConnection`/`Dapper` = só Functions + McpServer (.NET); `mssql` =
--   fifa2026-api (Node); Gateway e FlowEvents NÃO tocam SQL (FlowEvents consulta Log Analytics/Kusto).
-- =====================================================

SET NOCOUNT ON;
GO

-- ============ Trava de segurança: nunca rodar em master ============
-- Contained users FROM EXTERNAL PROVIDER pertencem ao banco da aplicação, não ao master.
IF DB_NAME() = N'master'
BEGIN
    RAISERROR('ABORTADO: conecte-se ao banco da aplicacao (FIFA2026Tickets) e rode novamente. Contained users NAO vao em master.', 16, 1);
    SET NOEXEC ON;   -- compila mas NÃO executa os batches seguintes (nada é criado em master)
END
GO

-- ============================================================================
-- Bloco A — Functions app (F1: Entry + Status + Consumer) → db_datawriter + db_datareader
--   Grava purchases (Consumer: PurchaseRepository.InsertPurchaseAsync) e lê (Status:
--   GetStatusByCorrelationIdAsync; o INSERT..SELECT também LÊ ticket_categories).
--   UMA MI para o app inteiro → papel = UNIÃO writer+reader (ver cabeçalho).
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'<mi-functions-app>')
    CREATE USER [<mi-functions-app>] FROM EXTERNAL PROVIDER;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datawriter' AND m.name = N'<mi-functions-app>'
)
    ALTER ROLE db_datawriter ADD MEMBER [<mi-functions-app>];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datareader' AND m.name = N'<mi-functions-app>'
)
    ALTER ROLE db_datareader ADD MEMBER [<mi-functions-app>];
GO

-- ============================================================================
-- Bloco B — McpServer → db_datareader  E NADA MAIS  (regra de ouro ADE-008 Inv 1)
--   FifaQueryRepository só faz SELECT. NENHUM outro role.
--   ATENÇÃO: adicionar db_datawriter/db_ddladmin/db_owner AQUI VIOLA a ADE-009 Inv 2 e a ADE-008 Inv 1.
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'<mi-mcpserver>')
    CREATE USER [<mi-mcpserver>] FROM EXTERNAL PROVIDER;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datareader' AND m.name = N'<mi-mcpserver>'
)
    ALTER ROLE db_datareader ADD MEMBER [<mi-mcpserver>];
GO

-- ============================================================================
-- Bloco C — Backend v1 (fifa2026-api, Node/Express) → db_datawriter + db_datareader
--   Acesso real verificado: CRUD completo (auth.js registra users; tickets.js grava purchases;
--   users/matches/stadiums/bracket fazem INSERT/UPDATE) + SELECT em várias rotas → writer+reader.
--
--   ATENÇÃO (forward-looking): na Story 4.1 o código do backend (src/config/database.js) AINDA usa
--   connection string (SQLAZURECONNSTR_/DB_PASSWORD) — NÃO foi convertido para MI nesta story.
--   Aplique este bloco quando @devops (a) habilitar a MI do App Service do backend e (b) trocar
--   database.js para AAD (o driver mssql/tedious suporta authentication:
--   'azure-active-directory-msi-app-service' / 'azure-active-directory-default'). Até lá, este
--   contained user apenas EXISTE, sem uso (inócuo) — ou @devops mantém o Bloco C comentado até a
--   conversão do database.js. É a mesma disciplina aditiva/idempotente dos demais blocos.
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'<mi-backend-v1>')
    CREATE USER [<mi-backend-v1>] FROM EXTERNAL PROVIDER;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datawriter' AND m.name = N'<mi-backend-v1>'
)
    ALTER ROLE db_datawriter ADD MEMBER [<mi-backend-v1>];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_role_members rm
    JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id
    JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id
    WHERE r.name = N'db_datareader' AND m.name = N'<mi-backend-v1>'
)
    ALTER ROLE db_datareader ADD MEMBER [<mi-backend-v1>];
GO

-- ============================================================================
-- Gateway (YARP) — NENHUM contained user. NÃO acessa SQL: é o guardião de perímetro (valida JWT,
--   injeta X-Correlation-ID/X-Entra-OID/X-Gateway-Key). A connection string SQL "permanece nas
--   Functions" (ADE-009 §Verificação; src/Fifa2026.V2.Gateway/Program.cs:49). NÃO criar usuário
--   para o gateway — dar-lhe acesso ao banco seria ampliar a superfície sem necessidade.
-- ============================================================================

-- ============ Validação ============
-- @devops confere o resultado esperado (contained users por MI + papéis mínimos):
--   <mi-functions-app> : db_datareader, db_datawriter
--   <mi-mcpserver>     : db_datareader                 (SÓ isso — regra de ouro; se aparecer mais, ERRO)
--   <mi-backend-v1>    : db_datareader, db_datawriter  (se o Bloco C foi aplicado)
SELECT
    dp.name        AS user_name,
    dp.type_desc   AS principal_type,   -- EXTERNAL_USER = mapeado a MI/AAD
    r.name         AS role_name
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members rm ON rm.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals   r  ON r.principal_id = rm.role_principal_id
WHERE dp.type IN ('E', 'X')             -- E = EXTERNAL_USER, X = EXTERNAL_GROUP
ORDER BY dp.name, r.name;

PRINT 'phase-08-contained-users.sql aplicada/verificada — esperado: contained users por MI com menor-privilegio (McpServer = db_datareader-only; Functions/Backend v1 = writer+reader). Smoke pos-deploy (Story 4.1 AC-8, @devops): um INSERT/UPDATE/DELETE via a MI do McpServer deve dar PERMISSAO NEGADA.';
GO

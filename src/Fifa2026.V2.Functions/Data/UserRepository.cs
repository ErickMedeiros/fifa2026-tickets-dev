using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Story 3.5 (ADE-007 v1.2 Invariante 8) — implementação Dapper + Microsoft.Data.SqlClient
/// das primitivas de <see cref="IUserRepository"/> sobre a tabela <c>users</c>.
/// TODAS as queries são parametrizadas (sem concatenação de string — anti SQL injection).
/// Reusa o groundwork da Story 2.11: <c>users.entra_oid</c> + índice UNIQUE filtrado
/// <c>UQ_users_entra_oid</c> (<c>phase-04-ciam-link.sql</c>) + <c>UQ_users_email</c>
/// (<c>schema.sql</c>) como guardas de idempotência — nenhuma DDL nova é introduzida.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    /// <summary>Código de erro do SQL Server para violação de unique/PK key.</summary>
    private const int SqlUniqueViolation = 2627;
    /// <summary>Violação de unique index (variante).</summary>
    private const int SqlDuplicateKey = 2601;

    /// <summary>
    /// ADE-007 v1.2 Invariante 8.1 — SENTINELA fail-closed para o <c>password</c> de um
    /// usuário nato-CIAM. <c>users.password</c> é <c>NVARCHAR(255) NOT NULL</c> sem
    /// <c>DEFAULT</c> (<c>schema.sql:20</c>) e um nato-CIAM NÃO tem bcrypt (a Microsoft
    /// gerencia a credencial — Inv 4: "o hash bcrypt não viaja"). Este valor:
    ///   • NÃO é um hash bcrypt válido (bcrypt = 60 chars começando com <c>$2a$/$2b$/$2y$</c>);
    ///   • logo <c>bcryptjs.compare(qualquer_entrada, SENTINELA)</c> resolve <c>false</c>
    ///     SEMPRE (<c>auth.js:95</c>) — o login v1 NUNCA autentica um nato-CIAM
    ///     (<b>fail-closed por construção</b>);
    ///   • é uma marca legível (não um valor aleatório opaco) para diagnóstico no banco.
    /// <c>UserRepositorySentinelTests</c> prova que a sentinela não conforma ao formato
    /// bcrypt (AC-5). Rejeitado tornar <c>password</c> nullable: seria <c>ALTER COLUMN</c>
    /// (não-aditivo — viola ADE-000 Inv 2) e relaxaria uma premissa do v1 homegrown (Inv 4).
    /// </summary>
    public const string CiamManagedPasswordSentinel = "CIAM_MANAGED__NO_LOCAL_PASSWORD";

    private readonly string _connectionString;
    private readonly ILogger<UserRepository> _logger;

    /// <summary>
    /// Mesma forma de <see cref="PurchaseRepository"/>: a connection string vem do App
    /// Setting <c>SqlConnectionString</c> (Managed Identity + Entra ID em runtime Azure —
    /// ADE-009 Inv 2 / Story 4.1; nunca no repo). Guard fail-closed se ausente.
    /// </summary>
    public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger)
    {
        _connectionString = configuration["SqlConnectionString"]
            ?? throw new InvalidOperationException(
                "App Setting 'SqlConnectionString' não configurado. Defina a connection string do SQL Server.");
        _logger = logger;
    }

    public async Task<int?> FindIdByEntraOidAsync(Guid entraOid, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (1) id
            FROM dbo.users
            WHERE entra_oid = @EntraOid;
            """;

        await using var connection = new SqlConnection(_connectionString);
        var command = new CommandDefinition(sql, new { EntraOid = entraOid }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int?>(command);
    }

    public async Task<int?> TryLinkByEmailAsync(Guid entraOid, string email, CancellationToken cancellationToken = default)
    {
        // UPDATE idempotente da migração de cadastro (Inv 6) executado on-demand: só vincula
        // quando a linha existe E ainda não tem oid (entra_oid IS NULL). OUTPUT devolve o id
        // vinculado; 0 linhas afetadas → nenhuma linha (ExecuteScalar → null). O índice
        // UNIQUE filtrado UQ_users_entra_oid garante que um oid não seja colado em 2 usuários.
        const string sql = """
            UPDATE dbo.users
            SET entra_oid = @EntraOid, updated_at = GETDATE()
            OUTPUT INSERTED.id
            WHERE email = @Email AND entra_oid IS NULL;
            """;

        await using var connection = new SqlConnection(_connectionString);

        try
        {
            var command = new CommandDefinition(sql, new { EntraOid = entraOid, Email = email }, cancellationToken: cancellationToken);
            return await connection.ExecuteScalarAsync<int?>(command);
        }
        catch (SqlException ex) when (ex.Number is SqlUniqueViolation or SqlDuplicateKey)
        {
            // Corrida rara: o mesmo oid tentando vincular a duas linhas (email) ao mesmo
            // tempo — UQ_users_entra_oid barra a segunda. Trata como "não vinculei" e deixa
            // a orquestração re-resolver por oid (que já terá o vínculo vencedor).
            _logger.LogInformation("Link por email colidiu com UQ_users_entra_oid (idempotência) — re-resolve.");
            return null;
        }
    }

    public async Task<int?> TryInsertCiamUserAsync(Guid entraOid, string email, string name, CancellationToken cancellationToken = default)
    {
        // role omitido de propósito → DEFAULT ('user') do schema aplica (AC-6, "sem ação").
        // created_at/updated_at idem (DF_users_created_at/updated_at). password = sentinela
        // fail-closed. Guardado por UQ_users_entra_oid E UQ_users_email.
        const string sql = """
            INSERT INTO dbo.users (name, email, password, entra_oid)
            OUTPUT INSERTED.id
            VALUES (@Name, @Email, @Password, @EntraOid);
            """;

        await using var connection = new SqlConnection(_connectionString);

        try
        {
            var command = new CommandDefinition(
                sql,
                new { Name = name, Email = email, Password = CiamManagedPasswordSentinel, EntraOid = entraOid },
                cancellationToken: cancellationToken);

            return await connection.ExecuteScalarAsync<int?>(command);
        }
        catch (SqlException ex) when (ex.Number is SqlUniqueViolation or SqlDuplicateKey)
        {
            // Idempotência sob concorrência (ADE-000 Inv 4): dois primeiros-logins do mesmo
            // oid/email colapsam em UMA linha; o perdedor recebe null e re-resolve. Espelha
            // PurchaseRepository.cs (o mesmo 2627 de UQ_purchases_correlation_id).
            _logger.LogInformation("INSERT JIT colidiu com índice UNIQUE (idempotência) — re-resolve.");
            return null;
        }
    }

    public async Task<UserIdentity?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (1) id AS Id, entra_oid AS EntraOid
            FROM dbo.users
            WHERE email = @Email;
            """;

        await using var connection = new SqlConnection(_connectionString);
        var command = new CommandDefinition(sql, new { Email = email }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserIdentity>(command);
    }
}

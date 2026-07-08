using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace Fifa2026.V2.Functions.Tests.Integration;

/// <summary>
/// Story 4.3 (EPIC-004) — sobe um SQL Server efêmero via <b>Testcontainers.MsSql</b> e
/// aplica o schema REAL (reúso, não recriação de DDL — Art. IV):
/// <c>fifa2026-api/database/schema.sql</c> + migrations <c>phase-01.sql</c>
/// (coluna <c>correlation_id</c> + índice UNIQUE filtrado <c>UQ_purchases_correlation_id</c>
/// — a defesa de idempotência sob teste), <c>phase-03.sql</c> (coluna <c>entra_oid</c> em
/// purchases) e <c>phase-04-ciam-link.sql</c> (coluna <c>users.entra_oid</c> +
/// <c>UQ_users_entra_oid</c>, insumo do resolve-or-provision de <c>UserRepository</c>), mais
/// um seed mínimo (1 match, 1 ticket_category 'VIP Premium', 1 user) para o INSERT resolver
/// via JOIN.
///
/// GRACEFUL DEGRADATION (AC-4): se o Docker não estiver disponível, <see cref="StartAsync"/>
/// falha; capturamos e marcamos <see cref="DockerAvailable"/> = false com um
/// <see cref="SkipReason"/>. Os testes de integração então se auto-pulam
/// (<c>Skip.IfNot</c>) em vez de ficar VERMELHOS — a suíte permanece verde sem Docker; o CI
/// (runner com Docker) roda de verdade.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    // Construído DENTRO de InitializeAsync (não no field initializer): MsSqlBuilder.Build()
    // valida o endpoint do Docker e LANÇA se ele não é resolvível — precisa estar sob o
    // try/catch para virar um skip gracioso, não um erro de construção do fixture (AC-4).
    private MsSqlContainer? _container;

    /// <summary>Connection string do container (banco <c>master</c>) — null se Docker indisponível.</summary>
    public string? ConnectionString { get; private set; }

    /// <summary>true quando o container subiu e o schema/seed foram aplicados.</summary>
    public bool DockerAvailable { get; private set; }

    /// <summary>Motivo do skip (quando o container não subiu) — exibido no resultado do teste.</summary>
    public string? SkipReason { get; private set; }

    // IDs semeados que os testes usam para satisfazer as FKs (purchases.user_id → users;
    // ticket_categories.match_id → matches). Capturados por SCOPE_IDENTITY no seed.
    public int SeededMatchId { get; private set; }
    public int SeededUserId { get; private set; }

    /// <summary>Código curto do contrato v2 (mapeado por CategoryLabelMapper para o rótulo do seed).</summary>
    public const string SeededCategoryContractCode = "VIP";
    /// <summary>Rótulo real gravado em <c>ticket_categories.category</c> (seed real das FIFA prices).</summary>
    public const string SeededCategoryDbLabel = "VIP Premium";

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder().Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            await ApplySchemaAndSeedAsync();
            DockerAvailable = true;
        }
        catch (Exception ex)
        {
            // Docker ausente/parado/misconfigurado (Build() ou StartAsync()) → não quebra a suíte.
            DockerAvailable = false;
            SkipReason = $"Docker/SQL Server container indisponível ({ex.GetType().Name}): {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is null)
        {
            return;
        }

        try
        {
            await _container.DisposeAsync();
        }
        catch
        {
            // O container pode nunca ter subido (Docker indisponível) — dispose é best-effort.
        }
    }

    private async Task ApplySchemaAndSeedAsync()
    {
        var databaseDir = LocateDatabaseDir();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Ordem importa: schema base (tabelas + FKs) → deltas aditivos das migrations.
        await ExecuteSqlFileAsync(connection, Path.Combine(databaseDir, "schema.sql"));
        await ExecuteSqlFileAsync(connection, Path.Combine(databaseDir, "migrations", "phase-01.sql"));
        await ExecuteSqlFileAsync(connection, Path.Combine(databaseDir, "migrations", "phase-03.sql"));
        await ExecuteSqlFileAsync(connection, Path.Combine(databaseDir, "migrations", "phase-04-ciam-link.sql"));

        // Seed mínimo. matches.home_team_id/away_team_id/stadium_id são NULL-able → omitidos.
        SeededMatchId = await connection.ExecuteScalarAsync<int>(
            "INSERT INTO dbo.matches (date, time, stage) " +
            "VALUES ('2026-06-11', '16:00', 'Fase de Grupos'); " +
            "SELECT CAST(SCOPE_IDENTITY() AS INT);");

        await connection.ExecuteAsync(
            "INSERT INTO dbo.ticket_categories (match_id, category, price, total_quantity, available_quantity) " +
            "VALUES (@MatchId, @Category, 100.00, 1000, 1000);",
            new { MatchId = SeededMatchId, Category = SeededCategoryDbLabel });

        SeededUserId = await connection.ExecuteScalarAsync<int>(
            "INSERT INTO dbo.users (name, email, password) " +
            "VALUES ('Seed User', 'seed@example.com', 'not-a-real-hash'); " +
            "SELECT CAST(SCOPE_IDENTITY() AS INT);");
    }

    private static async Task ExecuteSqlFileAsync(SqlConnection connection, string path)
    {
        var script = await File.ReadAllTextAsync(path);
        foreach (var batch in SplitOnGo(script))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await connection.ExecuteAsync(batch);
        }
    }

    /// <summary>
    /// Quebra o script nos separadores de lote <c>GO</c> (que NÃO são T-SQL válido para um
    /// único <c>SqlCommand</c> — são diretiva de sqlcmd/SSMS). Cada lote é executado à parte.
    /// </summary>
    private static IEnumerable<string> SplitOnGo(string script)
    {
        var builder = new StringBuilder();
        foreach (var line in script.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                yield return builder.ToString();
                builder.Clear();
            }
            else
            {
                builder.Append(line).Append('\n');
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    /// <summary>
    /// Sobe a árvore a partir do diretório de saída dos testes até achar
    /// <c>fifa2026-api/database/schema.sql</c> (a fonte real do schema no repo).
    /// </summary>
    private static string LocateDatabaseDir()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "fifa2026-api", "database");
            if (File.Exists(Path.Combine(candidate, "schema.sql")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Não localizei 'fifa2026-api/database/schema.sql' subindo a partir de {AppContext.BaseDirectory}.");
    }
}

/// <summary>
/// Compartilha UM container SQL Server entre as classes de teste de integração (evita subir
/// N containers no CI). As classes usam correlation_ids/emails únicos por teste → sem
/// interferência mesmo compartilhando o banco.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SqlServerIntegration";
}

using Dapper;
using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.Functions.Tests.Integration;

/// <summary>
/// Story 4.3 (EPIC-004) AC-2/AC-3 — idempotência SQL REAL do
/// <see cref="PurchaseRepository.InsertPurchaseAsync"/> contra um SQL Server efêmero
/// (Testcontainers), exercitando a violação REAL do índice UNIQUE filtrado
/// <c>UQ_purchases_correlation_id</c> (<c>SqlException 2627/2601</c>) — NÃO um mock de
/// exceção. Fecha a lacuna apontada pela ADE-009 (§Consequences): "teste de integração da
/// idempotência SQL real, hoje só e2e manual".
///
/// O <see cref="PurchaseRepositoryTests"/> (unitário, existente) cobre só o curto-circuito
/// <c>CategoryNotFound</c> ANTES de abrir conexão; ESTA classe cobre o caminho que toca o
/// banco de verdade. Sem Docker, os testes se auto-pulam (AC-4).
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PurchaseRepositoryIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;

    public PurchaseRepositoryIntegrationTests(SqlServerContainerFixture fixture) => _fixture = fixture;

    private PurchaseRepository BuildRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlConnectionString"] = _fixture.ConnectionString
            })
            .Build();

        return new PurchaseRepository(configuration, NullLogger<PurchaseRepository>.Instance);
    }

    private PurchaseMessage NewMessage(Guid correlationId) => new()
    {
        CorrelationId = correlationId,
        MatchId = _fixture.SeededMatchId,
        Category = SqlServerContainerFixture.SeededCategoryContractCode, // "VIP" → 'VIP Premium'
        UserId = _fixture.SeededUserId,
        Quantity = 1,
        EntraOid = null
    };

    [SkippableFact]
    public async Task Same_correlationId_twice_inserts_once_then_Duplicate()
    {
        Skip.IfNot(_fixture.DockerAvailable, _fixture.SkipReason ?? "Docker indisponível");

        var repository = BuildRepository();
        var correlationId = Guid.NewGuid();

        var first = await repository.InsertPurchaseAsync(NewMessage(correlationId));
        var second = await repository.InsertPurchaseAsync(NewMessage(correlationId));

        Assert.Equal(InsertOutcome.Inserted, first);
        // A 2ª chamada colide com UQ_purchases_correlation_id (2627) → Duplicate, não erro.
        Assert.Equal(InsertOutcome.Duplicate, second);
        // E a linha NÃO foi duplicada — o banco é o source-of-truth da unicidade.
        Assert.Equal(1, await CountByCorrelationIdAsync(correlationId));
    }

    [SkippableFact]
    public async Task Concurrent_same_correlationId_inserts_exactly_once()
    {
        Skip.IfNot(_fixture.DockerAvailable, _fixture.SkipReason ?? "Docker indisponível");

        var repository = BuildRepository();
        var correlationId = Guid.NewGuid();

        // Duas requests SIMULTÂNEAS com o mesmo correlationId (reentrega/at-least-once do
        // Service Bus, ou consumers paralelos). Prova que a defesa é a violação do índice
        // no banco (ADE-000 Inv 4 — NUNCA SELECT-then-INSERT / TOCTOU), não uma checagem prévia.
        var outcomes = await Task.WhenAll(
            repository.InsertPurchaseAsync(NewMessage(correlationId)),
            repository.InsertPurchaseAsync(NewMessage(correlationId)));

        Assert.Equal(1, outcomes.Count(o => o == InsertOutcome.Inserted));
        Assert.Equal(1, outcomes.Count(o => o == InsertOutcome.Duplicate));
        // Nunca "as duas Inserted", nunca uma exceção não tratada virando CategoryNotFound.
        Assert.DoesNotContain(InsertOutcome.CategoryNotFound, outcomes);
        Assert.Equal(1, await CountByCorrelationIdAsync(correlationId));
    }

    [SkippableFact]
    public async Task Distinct_correlationIds_each_insert_independently()
    {
        Skip.IfNot(_fixture.DockerAvailable, _fixture.SkipReason ?? "Docker indisponível");

        var repository = BuildRepository();

        // Sanidade: correlationIds DIFERENTES não são bloqueados pelo índice (o filtro é
        // por correlation_id; múltiplos NULL/valores distintos convivem). Garante que o
        // teste de duplicata acima falha por DUPLICATA, não por um bug no seed/JOIN.
        var first = await repository.InsertPurchaseAsync(NewMessage(Guid.NewGuid()));
        var second = await repository.InsertPurchaseAsync(NewMessage(Guid.NewGuid()));

        Assert.Equal(InsertOutcome.Inserted, first);
        Assert.Equal(InsertOutcome.Inserted, second);
    }

    private async Task<int> CountByCorrelationIdAsync(Guid correlationId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.purchases WHERE correlation_id = @CorrelationId;",
            new { CorrelationId = correlationId });
    }
}

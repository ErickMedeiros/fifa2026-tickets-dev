using Dapper;
using Fifa2026.V2.Functions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.Functions.Tests.Integration;

/// <summary>
/// Story 4.3 (EPIC-004) AC-2 (extensão ao <see cref="UserRepository"/>, Story 3.5) —
/// idempotência SQL REAL do resolve-or-provision (resolve por oid, link por email, insert
/// JIT) contra um SQL Server efêmero. Exercita a violação REAL de <c>UQ_users_entra_oid</c>
/// / <c>UQ_users_email</c> (<c>SqlException 2627/2601</c> → <c>null</c> = re-resolve), NÃO um
/// mock. Sem Docker, os testes se auto-pulam (AC-4).
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UserRepositoryIntegrationTests
{
    private readonly SqlServerContainerFixture _fixture;

    public UserRepositoryIntegrationTests(SqlServerContainerFixture fixture) => _fixture = fixture;

    private UserRepository BuildRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlConnectionString"] = _fixture.ConnectionString
            })
            .Build();

        return new UserRepository(configuration, NullLogger<UserRepository>.Instance);
    }

    private static string UniqueEmail() => $"ciam-{Guid.NewGuid():N}@example.com";

    [SkippableFact]
    public async Task Insert_JIT_is_idempotent_under_duplicate_oid()
    {
        Skip.IfNot(_fixture.DockerAvailable, _fixture.SkipReason ?? "Docker indisponível");

        var repository = BuildRepository();
        var oid = Guid.NewGuid();

        // 1º primeiro-login: provisiona a linha nato-CIAM.
        var firstId = await repository.TryInsertCiamUserAsync(oid, UniqueEmail(), "Cliente CIAM");
        // 2º INSERT com o MESMO oid (email diferente) → colide com UQ_users_entra_oid (2627)
        // → null (a orquestração re-resolve por oid), NUNCA uma 2ª linha nem exceção vazada.
        var secondId = await repository.TryInsertCiamUserAsync(oid, UniqueEmail(), "Cliente CIAM");

        Assert.NotNull(firstId);
        Assert.Null(secondId);
        // Re-resolve determinístico por oid devolve exatamente a linha vencedora.
        Assert.Equal(firstId, await repository.FindIdByEntraOidAsync(oid));
        Assert.Equal(1, await CountByEntraOidAsync(oid));
    }

    [SkippableFact]
    public async Task Concurrent_first_login_same_oid_provisions_exactly_one_row()
    {
        Skip.IfNot(_fixture.DockerAvailable, _fixture.SkipReason ?? "Docker indisponível");

        var repository = BuildRepository();
        var oid = Guid.NewGuid();
        var email = UniqueEmail();

        // Dois primeiros-logins SIMULTÂNEOS do mesmo oid/email colapsam em UMA linha
        // (ADE-000 Inv 4): exatamente um INSERT vence, o outro recebe null e re-resolve.
        var ids = await Task.WhenAll(
            repository.TryInsertCiamUserAsync(oid, email, "Cliente CIAM"),
            repository.TryInsertCiamUserAsync(oid, email, "Cliente CIAM"));

        Assert.Equal(1, ids.Count(id => id is not null));
        Assert.Equal(1, ids.Count(id => id is null));
        Assert.Equal(1, await CountByEntraOidAsync(oid));
    }

    [SkippableFact]
    public async Task Link_by_email_binds_v1_user_and_becomes_resolvable_by_oid()
    {
        Skip.IfNot(_fixture.DockerAvailable, _fixture.SkipReason ?? "Docker indisponível");

        var repository = BuildRepository();
        var email = UniqueEmail();
        var v1UserId = await InsertLegacyV1UserAsync(email);
        var oid = Guid.NewGuid();

        // Passo 2 do resolve-or-provision: vincula (UPDATE ... WHERE email AND entra_oid IS NULL).
        var linkedId = await repository.TryLinkByEmailAsync(oid, email);

        Assert.Equal(v1UserId, linkedId);
        // Depois do link, a MESMA linha é resolvível por oid (Passo 1).
        Assert.Equal(v1UserId, await repository.FindIdByEntraOidAsync(oid));

        var identity = await repository.FindByEmailAsync(email);
        Assert.NotNull(identity);
        Assert.Equal(v1UserId, identity!.Id);
        Assert.Equal(oid, identity.EntraOid);
    }

    [SkippableFact]
    public async Task Link_by_email_is_noop_when_already_linked()
    {
        Skip.IfNot(_fixture.DockerAvailable, _fixture.SkipReason ?? "Docker indisponível");

        var repository = BuildRepository();
        var email = UniqueEmail();
        await InsertLegacyV1UserAsync(email);

        var firstOid = Guid.NewGuid();
        var firstLink = await repository.TryLinkByEmailAsync(firstOid, email);
        // 2ª tentativa com OUTRO oid: a linha já tem entra_oid (o guard `entra_oid IS NULL`
        // do UPDATE não casa) → 0 linhas afetadas → null. Não rouba o vínculo existente.
        var secondLink = await repository.TryLinkByEmailAsync(Guid.NewGuid(), email);

        Assert.NotNull(firstLink);
        Assert.Null(secondLink);
    }

    private async Task<int> InsertLegacyV1UserAsync(string email)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "INSERT INTO dbo.users (name, email, password) VALUES ('Legacy V1', @Email, 'bcrypt-hash'); " +
            "SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { Email = email });
    }

    private async Task<int> CountByEntraOidAsync(Guid entraOid)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.users WHERE entra_oid = @EntraOid;",
            new { EntraOid = entraOid });
    }
}

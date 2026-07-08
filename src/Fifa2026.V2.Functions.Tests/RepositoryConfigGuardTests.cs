using Fifa2026.V2.Functions.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// Story 4.3 (EPIC-004) AC-9 — guard fail-closed de <c>SqlConnectionString</c> ausente nos
/// repositórios das Functions. Ambos os construtores DEVEM lançar
/// <see cref="InvalidOperationException"/> quando a App Setting não está configurada (em vez
/// de subir e falhar silenciosamente na 1ª query). Hoje NENHUM teste cobre esse caminho —
/// uma regressão que removesse o guard passaria despercebida. Teste unitário direto (sem
/// banco, sem WebApplicationFactory): basta instanciar com um <see cref="IConfiguration"/>
/// sem a chave.
/// </summary>
public sealed class RepositoryConfigGuardTests
{
    private static IConfiguration ConfigWithoutConnectionString() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

    [Fact]
    public void PurchaseRepository_ctor_throws_when_SqlConnectionString_missing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new PurchaseRepository(
                ConfigWithoutConnectionString(),
                NullLogger<PurchaseRepository>.Instance));

        Assert.Contains("SqlConnectionString", exception.Message);
    }

    [Fact]
    public void UserRepository_ctor_throws_when_SqlConnectionString_missing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new UserRepository(
                ConfigWithoutConnectionString(),
                NullLogger<UserRepository>.Instance));

        Assert.Contains("SqlConnectionString", exception.Message);
    }
}

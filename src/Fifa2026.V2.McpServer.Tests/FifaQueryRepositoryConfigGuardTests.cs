using Fifa2026.V2.McpServer.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// Story 4.3 (EPIC-004) AC-9 — guard fail-closed de <c>SqlConnectionString</c> ausente no
/// <see cref="FifaQueryRepository"/> (McpServer). O construtor DEVE lançar
/// <see cref="InvalidOperationException"/> quando a App Setting não está configurada, em vez
/// de subir e só falhar na 1ª consulta. Hoje nenhum teste cobre esse caminho.
/// </summary>
public sealed class FifaQueryRepositoryConfigGuardTests
{
    [Fact]
    public void FifaQueryRepository_ctor_throws_when_SqlConnectionString_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new FifaQueryRepository(
                configuration,
                NullLogger<FifaQueryRepository>.Instance));

        Assert.Contains("SqlConnectionString", exception.Message);
    }
}

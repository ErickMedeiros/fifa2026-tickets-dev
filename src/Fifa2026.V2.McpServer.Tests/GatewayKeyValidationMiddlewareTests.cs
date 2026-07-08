using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// Story 4.2 (ADE-009 Inv 1 / AC-7/AC-8/AC-12) — prova ponta-a-ponta da METADE "destino
/// rejeita sem a chave" da demo-dinheiro, via <see cref="WebApplicationFactory{TEntryPoint}"/>:
///   - segredo configurado + SEM header (ou header errado) → <b>401</b> (fecha o bypass);
///   - segredo configurado + header válido → passa da trava (não é 401);
///   - <c>/health</c> nunca exige o header (AC-8);
///   - segredo vazio → legado, passa-through (AC-12, retro-compat com labs sem gateway).
///
/// Usa <c>GATEWAY_SHARED_SECRET</c> via UseSetting (IConfiguration inclui host settings).
/// </summary>
public sealed class GatewayKeyValidationMiddlewareTests
{
    private const string Secret = "mcp-test-shared-secret-abc";
    private const string HeaderName = "X-Gateway-Key";

    private static WebApplicationFactory<Program> CreateFactory(string? secret) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (secret is not null)
            {
                builder.UseSetting("GATEWAY_SHARED_SECRET", secret);
            }
        });

    private static StringContent JsonRpc() =>
        new("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", Encoding.UTF8, "application/json");

    [Fact]
    public async Task SecretConfigured_NoHeader_OnMcp_Returns401()
    {
        using var factory = CreateFactory(Secret);
        var client = factory.CreateClient();

        var response = await client.PostAsync("/mcp", JsonRpc());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // É a NOSSA rejeição de gateway-key (não uma 401 de outra camada).
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("X-Gateway-Key", body);
    }

    [Fact]
    public async Task SecretConfigured_WrongHeader_OnMcp_Returns401()
    {
        using var factory = CreateFactory(Secret);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderName, "forged-evil-key");

        var response = await client.PostAsync("/mcp", JsonRpc());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecretConfigured_ValidHeader_OnMcp_PassesGate()
    {
        using var factory = CreateFactory(Secret);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderName, Secret);

        var response = await client.PostAsync("/mcp", JsonRpc());

        // Passou da trava (chegou no handler MCP) → NÃO é a nossa 401.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecretConfigured_Health_NoHeader_Returns200()
    {
        using var factory = CreateFactory(Secret);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        // AC-8: o health probe do Container App não exige X-Gateway-Key.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmptySecret_NoHeader_OnMcp_PassesGate_LegacyRetroCompat()
    {
        using var factory = CreateFactory(string.Empty); // trava desligada
        var client = factory.CreateClient();

        var response = await client.PostAsync("/mcp", JsonRpc());

        // AC-12: sem segredo, o McpServer se comporta como hoje (não exige header).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

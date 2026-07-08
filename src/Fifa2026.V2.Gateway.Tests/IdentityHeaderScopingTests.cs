using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 3.5 / M-1 (code review 2026-07-01, minimização de PII) — a injeção de
/// X-Entra-Email/X-Entra-Name (PII do cliente CIAM) é ESCOPADA à rota me-get (GET /api/v2/me),
/// o único consumidor. Antes era GLOBAL: todos os clusters (purchase, mcp, flow-events...)
/// recebiam a PII sem precisar. Estes testes provam o escopo: a rota /me recebe email/name; a
/// rota /purchase (MESMO cluster functions-f1, MESMO token CIAM) recebe só o X-Entra-OID
/// (global — contrato Story 2.3), NUNCA email/name.
///
/// Mesmo padrão de <see cref="GatewayKeyInjectionTests"/> (WebApplicationFactory + WireMock,
/// inspeção dos headers efetivamente encaminhados ao backend). ≤ 5 requisições (partição de
/// cliente, 5/min por IP).
/// </summary>
public sealed class IdentityHeaderScopingTests : IClassFixture<GatewayTestFixture>
{
    private const string MeBackendPath = "/api/v2/me";
    private const string PurchaseBackendPath = "/api/v2/purchase";

    private readonly GatewayTestFixture _fixture;

    public IdentityHeaderScopingTests(GatewayTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MeRoute_ForwardsEmailAndName_WhenEmailVerified()
    {
        // Contraparte positiva do escopo: SÓ a rota /me recebe a PII (email/name verificados).
        _fixture.Backend
            .Given(Request.Create().WithPath(MeBackendPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "application/json").WithBody("{\"userId\":1}"));

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokenFactory.Create(
                oid: "dddddddd-1111-2222-3333-444444444444",
                email: "cliente@example.com", name: "Cliente CIAM", emailVerified: true));

        var response = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = _fixture.Backend.LogEntries.Last(e => e.RequestMessage.Path == MeBackendPath);
        var headers = log.RequestMessage.Headers!;
        Assert.True(headers.ContainsKey("X-Entra-OID"));
        Assert.True(headers.ContainsKey("X-Entra-Email"));
        Assert.True(headers.ContainsKey("X-Entra-Name"));
    }

    [Fact]
    public async Task PurchaseRoute_DoesNotForwardEmailOrName_OnlyOid()
    {
        // O CORAÇÃO do M-1: o MESMO token CIAM (com email verificado) numa rota que NÃO é /me
        // (purchase, mesmo cluster functions-f1) NÃO vaza email/name — só o X-Entra-OID (global).
        _fixture.Backend
            .Given(Request.Create().WithPath(PurchaseBackendPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202)
                .WithHeader("Content-Type", "application/json").WithBody("{\"status\":\"queued\"}"));

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokenFactory.Create(
                oid: "eeeeeeee-1111-2222-3333-444444444444",
                email: "cliente@example.com", name: "Cliente CIAM", emailVerified: true));

        var response = await client.PostAsJsonAsync("/purchase",
            new { matchId = 1, category = "VIP", userId = 1, quantity = 1 });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var log = _fixture.Backend.LogEntries.Last(e => e.RequestMessage.Path == PurchaseBackendPath);
        var headers = log.RequestMessage.Headers!;
        Assert.True(headers.ContainsKey("X-Entra-OID"));    // contrato global (Story 2.3) preservado
        Assert.False(headers.ContainsKey("X-Entra-Email")); // M-1: PII escopada a /me — não vaza
        Assert.False(headers.ContainsKey("X-Entra-Name"));  // idem
    }
}

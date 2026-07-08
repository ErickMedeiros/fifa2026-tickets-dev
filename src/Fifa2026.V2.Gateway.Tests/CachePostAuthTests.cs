using System.Net;
using System.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 4.4 (AC-2/AC-4 / ADE-009 §Consequences — P1 de segurança) — o
/// <c>XCacheMiddleware</c> roda DEPOIS de <c>UseAuthentication</c>/<c>UseAuthorization</c>.
///
/// Cenário central ("cache HIT só com auth"): um cache HIT NUNCA serve o status de uma compra
/// sem token válido. Antes do reorder da Story 4.4, o cache fazia short-circuit ANTES do auth —
/// uma vez populado por um cliente autenticado, um segundo GET SEM token dentro da janela de 30s
/// recebia o 200 cacheado (bypass de autenticação por 30s). Após o reorder, toda request passa
/// por auth primeiro: o GET sem token dá 401 e NUNCA alcança o cache.
///
/// Classe própria (fixture própria) de propósito: isola o bucket "por IP" do rate-limiter das
/// demais classes de teste (evita acoplamento de contadores 5/min entre arquivos).
/// </summary>
public sealed class CachePostAuthTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public CachePostAuthTests(GatewayTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CacheHit_IsNeverServed_WithoutAuth_AfterReorder()
    {
        // Arrange — backend responde 200 na rota de status.
        var correlationId = "44444444-4444-4444-4444-444444444444";
        _fixture.Backend
            .Given(Request.Create()
                .WithPath($"/api/v2/purchase/{correlationId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"status\":\"completed\"}"));

        var path = $"/purchase/{correlationId}";

        // 1) Cliente AUTENTICADO popula o cache (MISS → 200 → store).
        var authedClient = _fixture.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create());

        var seeded = await authedClient.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        Assert.Equal("MISS", seeded.Headers.GetValues("X-Cache").Single());

        // 2) Cliente SEM token, MESMO path, dentro da janela de 30s.
        //    ANTES da Story 4.4: HIT serviria o 200 cacheado SEM auth (o bug).
        //    DEPOIS: auth roda ANTES do cache → 401, e o cache nem é alcançado.
        var anonClient = _fixture.CreateClient();
        var anon = await anonClient.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        // 3) Cliente autenticado de novo → HIT (o cache continua valendo para quem tem auth).
        var second = await authedClient.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("HIT", second.Headers.GetValues("X-Cache").Single());

        // O backend foi chamado UMA única vez: o request anônimo nem chegou nele (401 antes do
        // proxy) e o 2º autenticado veio do cache.
        var backendCalls = _fixture.Backend.LogEntries
            .Count(e => e.RequestMessage.Path == $"/api/v2/purchase/{correlationId}");
        Assert.Equal(1, backendCalls);
    }
}

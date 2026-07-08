using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 4.4 (AC-6/AC-7/AC-8 / ADE-009 §Consequences) — <c>UseForwardedHeaders</c> faz o
/// rate-limiter particionar pelo IP REAL do cliente (via <c>X-Forwarded-For</c> do ingress),
/// não pelo IP do ingress do Container Apps (que colapsaria todo o tráfego num único bucket).
///
/// Duas origens de IP distintas (simuladas por <c>X-Forwarded-For</c> diferente) NÃO
/// compartilham o contador de 5/min: um IP que esgotou o limite não bloqueia o outro.
///
/// SEM o fix (RemoteIpAddress = null/ingress p/ todos): as duas origens cairiam no MESMO bucket
/// "unknown" e o IP B seria bloqueado junto com o A — este teste captura a correção.
///
/// Fixture própria: isola os contadores de rate-limit das demais classes.
/// </summary>
public sealed class ForwardedHeadersRateLimitTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public ForwardedHeadersRateLimitTests(GatewayTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RateLimit_PartitionsBy_RealClientIp_FromXForwardedFor()
    {
        // Arrange — backend sempre 202 (isola o rate-limiter).
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/v2/purchase").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBody("{\"correlationId\":\"x\",\"status\":\"queued\"}"));

        var token = TestTokenFactory.Create();

        // Endereços TEST-NET-3 (RFC 5737 — reservados para documentação/teste).
        const string ipA = "203.0.113.10";
        const string ipB = "203.0.113.20";

        // IP A esgota o limite de 5/min (5 passam, a 6ª é bloqueada).
        for (var i = 1; i <= 5; i++)
        {
            var ok = await SendPurchase(token, forwardedFor: ipA);
            Assert.Equal(HttpStatusCode.Accepted, ok.StatusCode);
        }
        var blockedA = await SendPurchase(token, forwardedFor: ipA);
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedA.StatusCode);

        // IP B (X-Forwarded-For distinto) tem o PRÓPRIO bucket — não afetado pelo esgotamento de A.
        var okB = await SendPurchase(token, forwardedFor: ipB);
        Assert.Equal(HttpStatusCode.Accepted, okB.StatusCode);
    }

    private async Task<HttpResponseMessage> SendPurchase(string token, string forwardedFor)
    {
        var client = _fixture.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/purchase")
        {
            Content = JsonContent.Create(new { matchId = 1, category = "VIP", userId = 1, quantity = 1 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return await client.SendAsync(request);
    }
}

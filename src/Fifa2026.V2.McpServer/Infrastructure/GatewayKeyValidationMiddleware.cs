using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Fifa2026.V2.McpServer.Infrastructure;

/// <summary>
/// Story 4.2 (ADE-009 Inv 1) — trava de confiança serviço-a-serviço no McpServer. O McpServer
/// nunca revalida o JWT (confia no gateway — ADE-004), e a ADE-009 aponta que NÃO há IaC
/// versionado garantindo <c>ingress: internal</c> para ele — então o <c>X-Gateway-Key</c> é o
/// PISO enforçável em código, independente de o isolamento de rede estar versionado ou não.
///
/// Exige que a request carregue o <c>X-Gateway-Key</c> real (injetado pelo gateway no cluster
/// <c>mcp-server</c>) quando o segredo está configurado — fail-closed → 401; legado (passa
/// direto) quando o segredo está vazio. Semântica idêntica ao <c>gatewayTrust.js</c>; a
/// decisão vive em <see cref="GatewayKeyValidator"/>.
///
/// Mesmo estilo estrutural do <see cref="XCacheMiddleware"/> do gateway
/// (<c>Fifa2026.V2.Gateway.Infrastructure</c>): construtor recebe só o <see cref="RequestDelegate"/>,
/// dependência de escopo/singleton (<see cref="IConfiguration"/>) entra por method injection em
/// <see cref="InvokeAsync"/>. Registrado ANTES de <c>MapMcp("/mcp")</c> em <c>Program.cs</c>,
/// cobrindo também <c>/llm/*</c> (mesmo cluster mcp-server). O <c>/health</c> fica de fora
/// (AC-8 — probe do Container App não deve exigir segredo de aplicação).
/// </summary>
public sealed class GatewayKeyValidationMiddleware
{
    private const string UnauthorizedBody = "{\"error\":\"X-Gateway-Key ausente ou invalido.\"}";

    private readonly RequestDelegate _next;

    public GatewayKeyValidationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        // Health probe nunca exige o header (AC-8) — o Container App precisa alcançar /health
        // sem passar pelo gateway.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var configuredSecret = configuration[GatewayKeyValidator.SecretConfigKey];
        var incomingKey = context.Request.Headers[GatewayKeyValidator.HeaderName].ToString();

        if (GatewayKeyValidator.Evaluate(configuredSecret, incomingKey) == GatewayTrustDecision.Rejected)
        {
            // Fail-closed: segredo configurado + header ausente/divergente → 401 (não chama
            // _next → a rota /mcp | /llm NÃO é processada).
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(UnauthorizedBody);
            return;
        }

        // LegacyBypass (segredo vazio) ou Trusted (header válido): segue no pipeline.
        await _next(context);
    }
}

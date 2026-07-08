using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;

namespace Fifa2026.V2.Functions.Infrastructure;

/// <summary>
/// Story 4.2 (ADE-009 Inv 1) — trava de confiança serviço-a-serviço nas Functions HTTP F1.
/// Fecha o P0 do bypass: hoje <c>PurchaseEntryFunction</c>/<c>PurchaseStatusFunction</c> são
/// <c>AuthorizationLevel.Anonymous</c> e confiam no header <c>X-Entra-OID</c> sem validar
/// token (protegidas só por obscuridade de URL). Este middleware do worker isolado exige que
/// a request HTTP carregue o <c>X-Gateway-Key</c> real (injetado pelo gateway) quando o
/// segredo está configurado — um <c>curl</c> forjando <c>X-Entra-OID</c> DIRETO na Function
/// (sem passar pelo gateway) recebe <b>401</b>; via gateway recebe <b>200</b> (a demo-dinheiro).
///
/// Semântica idêntica ao <c>gatewayTrust.js</c> (comparação timing-safe, fail-closed quando
/// configurado, legado quando vazio) — a decisão vive em <see cref="GatewayKeyValidator"/>.
///
/// ESCOPO: só invocações HTTP-triggered. A invocação Service Bus-triggered de
/// <c>PurchaseConsumerFunction</c> NÃO tem <see cref="HttpContext"/> (<see cref="FunctionContext"/>.
/// <c>GetHttpContext()</c> retorna null) → passa direto, sem exigir header. O gateway continua
/// o guardião ÚNICO do JWT (ADE-004 preservada): isto prova a ORIGEM, não valida o token.
/// </summary>
public sealed class GatewayKeyValidationMiddleware : IFunctionsWorkerMiddleware
{
    private const string UnauthorizedBody = "{\"error\":\"X-Gateway-Key ausente ou invalido.\"}";

    private readonly IConfiguration _configuration;

    public GatewayKeyValidationMiddleware(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        // Não-HTTP (Service Bus-triggered PurchaseConsumerFunction etc.): sem headers HTTP,
        // a trava não se aplica — passa direto para a Function.
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        // Health probe (se existir) nunca exige o header (AC-8). Hoje as Functions não
        // expõem /health HTTP, mas mantemos o piso de plataforma (probe do Function App).
        if (IsHealthProbe(httpContext.Request.Path))
        {
            await next(context);
            return;
        }

        var configuredSecret = _configuration[GatewayKeyValidator.SecretConfigKey];
        var incomingKey = httpContext.Request.Headers[GatewayKeyValidator.HeaderName].ToString();

        if (GatewayKeyValidator.Evaluate(configuredSecret, incomingKey) == GatewayTrustDecision.Rejected)
        {
            // Fail-closed: segredo configurado + header ausente/divergente → 401. NÃO
            // chamamos next → a Function (e seu output binding do Service Bus) NÃO executa:
            // um curl forjado nunca vira mensagem na fila `tickets-purchase`.
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(UnauthorizedBody);
            return;
        }

        // LegacyBypass (segredo vazio → labs sem gateway) ou Trusted (header válido): segue.
        await next(context);
    }

    private static bool IsHealthProbe(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/api/health");
}

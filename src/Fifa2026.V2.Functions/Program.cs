using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// .NET 8 isolated worker entrypoint.
// ConfigureFunctionsWebApplication enables ASP.NET Core integration
// (HttpRequest/IActionResult) in HTTP triggers.
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerBuilder =>
    {
        // Story 4.2 (ADE-009 Inv 1) — trava X-Gateway-Key nas Functions HTTP F1 (fecha o
        // P0 do bypass). Só invocações HTTP-triggered são avaliadas; a Service Bus-triggered
        // (PurchaseConsumerFunction) passa direto (sem HttpContext). Gating por config
        // (segredo vazio = legado, preserva labs sem gateway — Oitavas/F1).
        workerBuilder.UseMiddleware<GatewayKeyValidationMiddleware>();
    })
    .ConfigureServices(services =>
    {
        // Application Insights — distributed tracing (W3C Trace Context, ADE-000 Inv 5).
        // The Activity API is fed automatically by the Functions/ServiceBus SDKs.
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // SQL purchase repository (parameterized queries via Dapper + Microsoft.Data.SqlClient).
        services.AddSingleton<IPurchaseRepository, PurchaseRepository>();

        // Story 3.5 (ADE-007 Inv 8) — repositório de users para o resolve-or-provision do
        // GET /api/v2/me (unificação base v1 ↔ CIAM). Mesmo padrão Dapper + SqlClient.
        services.AddSingleton<IUserRepository, UserRepository>();

        // Story 3.1 (ADE-008 Inv 3) — a notificação pós-compra agora é INLINE no consumer
        // (log estruturado correlacionado), sem orquestração externa. Não há mais webhook/
        // HttpClient de saída nas Functions — o registro do notifier n8n foi removido.
    })
    .ConfigureLogging(logging =>
    {
        // GOTCHA do isolated worker: AddApplicationInsightsTelemetryWorkerService aplica uma
        // regra de filtro PADRÃO que limita o provider do Application Insights a Warning,
        // descartando os logs Information do worker (nossos ILogger.LogInformation com
        // BeginScope de OrderId/correlationId) ANTES de chegarem ao App Insights. O host.json
        // controla o HOST, não esse filtro do worker — por isso os logs de aplicação não
        // apareciam. Removemos a regra para que a telemetria de aplicação (rastreio ponta-a-
        // ponta por correlationId/orderId) chegue ao App Insights.
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            var aiRule = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (aiRule is not null)
            {
                options.Rules.Remove(aiRule);
            }
        });
    })
    .Build();

host.Run();

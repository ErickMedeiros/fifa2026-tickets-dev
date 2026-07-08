using Fifa2026.V2.FlowEvents.Data;
using Fifa2026.V2.FlowEvents.Models;
using Xunit;

namespace Fifa2026.V2.FlowEvents.Tests;

/// <summary>
/// AC-4/AC-13 — classificação de traces do App Insights nos 5 hops REAIS (ADE-008 Inv 5 —
/// o nó do n8n foi removido; a notificação pós-compra é inline no nó Consumer). O NÓ ZERO
/// é o Gateway YARP — NUNCA APIM. Estes testes são a barreira anti-regressão contra a
/// narrativa antiga (APIM como nó zero) e contra o nó n8n ressuscitar.
/// </summary>
public sealed class TraceEventMapperTests
{
    [Theory]
    [InlineData("fifa2026-gateway", "X-Correlation-ID injetado", FlowEventType.GATEWAY_YARP_RECEIVED)]
    [InlineData("fifa2026-functions", "Compra v2 recebida: matchId=1", FlowEventType.FUNCTION_ENTRY_PROCESSED)]
    [InlineData("fifa2026-functions", "Mensagem publicada em tickets-purchase", FlowEventType.SERVICE_BUS_PUBLISHED)]
    [InlineData("fifa2026-functions", "Processando compra v2: matchId=1", FlowEventType.FUNCTION_CONSUMER_DONE)]
    [InlineData("fifa2026-functions", "Compra v2 gravada com sucesso", FlowEventType.SQL_INSERTED)]
    public void Classify_maps_real_hops(string role, string message, FlowEventType expected)
    {
        Assert.Equal(expected, TraceEventMapper.Classify(role, message));
    }

    [Fact]
    public void Gateway_also_detected_via_yarp_role_and_request()
    {
        // Segunda via de detecção do nó 0 (role yarp + "request"), distinta do sinal
        // "x-correlation-id" da via canônica acima — preserva a cobertura do branch.
        Assert.Equal(
            FlowEventType.GATEWAY_YARP_RECEIVED,
            TraceEventMapper.Classify("yarp-gateway", "request recebida"));
    }

    [Fact]
    public void Inline_post_purchase_notification_is_not_a_hop()
    {
        // ADE-008 Inv 5 — a notificação pós-compra inline NÃO é um nó próprio: seu trace
        // não classifica em nenhum hop (fica "dobrada" no nó Consumer). Também garante que
        // o branch n8n foi de fato removido (a mensagem não ressuscita o nó 4 antigo).
        var result = TraceEventMapper.Classify(
            "fifa2026-functions",
            "Notificação pós-compra enviada (inline, sem orquestração externa): matchId=1 category=VIP (correlationId=abc).");
        Assert.Null(result);
    }

    [Fact]
    public void Node_zero_is_gateway_yarp_never_apim()
    {
        // AC-13 — a arquitetura real NÃO tem APIM. Um trace "apim" não deve mapear ao nó 0
        // (nem a nenhum hop), garantindo que a narrativa antiga não ressuscite.
        var result = TraceEventMapper.Classify("legacy-apim", "APIM policy executed");
        Assert.Null(result);

        // E o gateway YARP é, sim, o nó 0 (ordinal 0).
        Assert.Equal(0, (int)FlowEventType.GATEWAY_YARP_RECEIVED);
    }

    [Fact]
    public void Unknown_trace_returns_null()
    {
        Assert.Null(TraceEventMapper.Classify("fifa2026-gateway-healthprobe", "GET /health 200"));
        Assert.Null(TraceEventMapper.Classify(null, null));
    }

    [Theory]
    [InlineData(0, "ok")]
    [InlineData(1, "ok")]
    [InlineData(2, "ok")]
    [InlineData(3, "error")]
    [InlineData(4, "error")]
    public void StatusFromSeverity_marks_error_at_3_or_above(int severity, string expected)
    {
        Assert.Equal(expected, TraceEventMapper.StatusFromSeverity(severity));
    }

    [Fact]
    public void All_five_event_types_have_distinct_sequential_node_indexes()
    {
        // Garante 5 nós, ordinais 0..4 sem buracos (a animação da bolinha depende disso;
        // ADE-008 Inv 5 renumerou SQL_INSERTED de 5 → 4 ao remover o nó n8n).
        var indexes = Enum.GetValues<FlowEventType>().Select(t => (int)t).OrderBy(i => i).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, indexes);
    }
}

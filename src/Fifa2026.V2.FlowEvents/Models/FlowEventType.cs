namespace Fifa2026.V2.FlowEvents.Models;

/// <summary>
/// Os 5 tipos de evento do Flow Visualizer (Story 3.1 / ADE-008 Inv 5 — o nó do n8n foi
/// removido; a notificação pós-compra ficou inline no nó Consumer). A ORDEM dos membros É a
/// ordem dos hops no diagrama — o NÓ ZERO é o Gateway YARP (ADE-004), NUNCA APIM (APIM não
/// existe no EPIC-002).
///
/// Cada membro corresponde a 1 nó do diagrama frontend (FlowDiagram). O índice
/// ordinal (0..4) é o número do nó usado pela animação da "bolinha".
/// </summary>
public enum FlowEventType
{
    /// <summary>Nó 0 — Gateway YARP recebe a request e injeta X-Correlation-ID (nó zero do tracing).</summary>
    GATEWAY_YARP_RECEIVED = 0,

    /// <summary>Nó 1 — PurchaseEntryFunction processa e publica no Service Bus.</summary>
    FUNCTION_ENTRY_PROCESSED = 1,

    /// <summary>Nó 2 — mensagem publicada na fila tickets-purchase do Service Bus.</summary>
    SERVICE_BUS_PUBLISHED = 2,

    /// <summary>Nó 3 — PurchaseConsumerFunction consome, grava no SQL e emite a notificação pós-compra inline (ADE-008 Inv 3).</summary>
    FUNCTION_CONSUMER_DONE = 3,

    /// <summary>Nó 4 — linha gravada em purchases.correlation_id no SQL.</summary>
    SQL_INSERTED = 4
}

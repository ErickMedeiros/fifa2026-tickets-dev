// =============================================================================
// Story 2.6 / F6 — Os 5 NÓS do Flow Visualizer (fonte única da verdade do diagrama).
// ADE-008 Inv 5 (Story 3.1): o nó do n8n foi removido e a notificação pós-compra ficou
// INLINE no nó Function Consumer (sem orquestração externa) — 6 → 5 nós.
//
// A ORDEM e os rótulos refletem a arquitetura REAL implementada nas fases anteriores
// (ADE-004). O NÓ ZERO é o Gateway YARP — NUNCA APIM (APIM não existe no EPIC-002).
//
//   [0] Gateway YARP     → injeta X-Correlation-ID (nó zero do tracing)
//   [1] Function Entry   → PurchaseEntryFunction (publica no Service Bus)
//   [2] Service Bus      → tickets-purchase
//   [3] Function Consumer→ PurchaseConsumerFunction (grava SQL + notificação pós-compra inline)
//   [4] SQL              → purchases.correlation_id
// =============================================================================

import type { FlowEventType } from '@/lib/flowApi';

export interface FlowNodeMeta {
  /** Índice ordinal 0..4 (posição na animação). */
  index: number;
  /** Tipo de evento que ativa este nó (bate 1:1 com o backend). */
  eventType: FlowEventType;
  /** Rótulo curto exibido no diagrama. */
  label: string;
  /** Descrição didática (Sheet de inspeção + aria-label — AC-9). */
  description: string;
  /** Nome do ícone lucide-react usado no nó. */
  icon: 'ShieldCheck' | 'Zap' | 'Mailbox' | 'Cog' | 'Workflow' | 'Database';
}

/** Os 5 nós, em ordem. Imutável (fonte única para FlowDiagram e animação). */
export const FLOW_NODES: readonly FlowNodeMeta[] = [
  {
    index: 0,
    eventType: 'GATEWAY_YARP_RECEIVED',
    label: 'Gateway YARP',
    description:
      'Nó zero do tracing. O gateway YARP (ASP.NET Core + Yarp.ReverseProxy) valida o JWT Entra, ' +
      'gera/propaga o X-Correlation-ID e encaminha a request à Function de entrada.',
    icon: 'ShieldCheck',
  },
  {
    index: 1,
    eventType: 'FUNCTION_ENTRY_PROCESSED',
    label: 'Function Entry',
    description:
      'PurchaseEntryFunction: lê o X-Correlation-ID do gateway, valida a compra e publica a ' +
      'mensagem na fila do Service Bus (tickets-purchase).',
    icon: 'Zap',
  },
  {
    index: 2,
    eventType: 'SERVICE_BUS_PUBLISHED',
    label: 'Service Bus',
    description:
      'Fila tickets-purchase: desacopla entrada e processamento. A mensagem carrega o ' +
      'correlationId no corpo para propagação ponta-a-ponta.',
    icon: 'Mailbox',
  },
  {
    index: 3,
    eventType: 'FUNCTION_CONSUMER_DONE',
    label: 'Function Consumer',
    description:
      'PurchaseConsumerFunction: consome a mensagem, grava a compra no SQL de forma idempotente ' +
      'e emite a notificação pós-compra INLINE (log correlacionado, sem orquestração externa — ' +
      'ADE-008 Inv 3). A notificação fica "dobrada" neste nó, não ganha nó próprio.',
    icon: 'Cog',
  },
  {
    index: 4,
    eventType: 'SQL_INSERTED',
    label: 'SQL',
    description:
      'Tabela purchases: linha gravada com correlation_id. Fim do fluxo — a compra está ' +
      'persistida e rastreável do gateway (nó 0) até aqui.',
    icon: 'Database',
  },
] as const;

/** Mapa eventType → índice do nó (para casar eventos recebidos com a animação). */
export const EVENT_TYPE_TO_INDEX: Record<FlowEventType, number> = FLOW_NODES.reduce(
  (acc, node) => {
    acc[node.eventType] = node.index;
    return acc;
  },
  {} as Record<FlowEventType, number>,
);

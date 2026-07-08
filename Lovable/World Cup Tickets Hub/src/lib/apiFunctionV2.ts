// =============================================================================
// Oitavas de Final — Cliente da compra ASSÍNCRONA via Azure Function v2 (F1).
//
// Paralelo a:
//   - src/lib/api.ts    (v1 Node/Express, compra SÍNCRONA — INTOCADO)
//   - src/lib/apiV2.ts  (gateway YARP + Bearer MSAL — NÃO usado aqui; aquele
//                        fluxo exige F2/F3 inexistentes neste ambiente)
//
// Esta Function tem authLevel Anonymous (sem token). O contrato é assíncrono e
// processa o CARRINHO INTEIRO (fan-out: 1 POST → N mensagens, 1 por linha):
//   POST /api/v2/purchase            → 202 { orderId, status, correlationIds[], correlationId? }
//   GET  /api/v2/purchase/{id}       → { status: "completed" | "failed" | ... }
//
// Base URL via VITE_FUNCTION_V2_URL (nunca hardcoded). A presença dessa var é o
// próprio toggle do fluxo v2 no Checkout.
// =============================================================================

import { getV2AccessToken } from '@/lib/authV2';

const FUNCTION_V2_URL = import.meta.env.VITE_FUNCTION_V2_URL ?? '';

/** Categorias aceitas pela Function v2 (enum estável, independente do label do banco — defeito M-1). */
export type PurchaseV2Category = 'VIP' | 'Cat1' | 'Cat2';

/** Uma linha do carrinho enviada ao envelope v2. */
export interface PurchaseV2Item {
  matchId: number;
  category: PurchaseV2Category;
  quantity: number;
}

/** Envelope do POST /api/v2/purchase — carrinho inteiro. */
export interface PurchaseFunctionV2Request {
  userId: number;
  items: PurchaseV2Item[];
}

export interface PurchaseFunctionV2Accepted {
  orderId: string;
  status: string; // esperado: "queued"
  correlationIds: string[];
  /** Presente apenas quando há exatamente 1 item (== correlationIds[0]). */
  correlationId?: string;
}

export interface PurchaseStatusV2 {
  status: string; // "completed" | "failed" | "processing" | "queued" | ...
  // O backend pode incluir campos extras; preservamos para uso futuro sem quebrar o contrato.
  [key: string]: unknown;
}

export interface FunctionV2Result<T> {
  data?: T;
  error?: string;
  /** HTTP status, útil para diagnóstico. */
  httpStatus?: number;
}

// Story 4.2 (ADE-009) — a Function F1 fica atrás do gateway após o hardening
// (GatewayKeyValidationMiddleware exige X-Gateway-Key, que só o gateway injeta).
// Chamadas diretas ao FUNCTION_V2_URL respondem 401. Roteamos por gateway com
// Bearer CIAM. FUNCTION_V2_URL segue como INTERRUPTOR do fluxo v2 (Checkout.tsx),
// mas a base HTTP real é a do gateway (VITE_GATEWAY_V2_URL).
const GATEWAY_V2_URL = import.meta.env.VITE_GATEWAY_V2_URL ?? '';

function normalizeBase(): string {
  return GATEWAY_V2_URL.replace(/\/+$/, '');
}

/**
 * POST /purchase (gateway → /api/v2/purchase na Function) — enfileira a compra.
 * Requer Bearer CIAM (o gateway rejeita 401 sem token válido).
 */
export async function purchaseViaFunction(
  body: PurchaseFunctionV2Request
): Promise<FunctionV2Result<PurchaseFunctionV2Accepted>> {
  const token = await getV2AccessToken();
  if (!token) {
    return { error: 'Sem sessão CIAM. Faça o "Login v2" (Entra) antes de comprar pelo fluxo v2.' };
  }

  try {
    const response = await fetch(`${normalizeBase()}/purchase`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      return {
        error: `Falha ao enfileirar a compra (HTTP ${response.status}).`,
        httpStatus: response.status,
      };
    }

    const data = (await response.json()) as PurchaseFunctionV2Accepted;
    return { data, httpStatus: response.status };
  } catch (error) {
    console.error('Function v2 (purchase) error:', error);
    return { error: 'Erro de conexão com o gateway v2.' };
  }
}

/**
 * GET /purchase/{correlationId} (gateway → /api/v2/purchase/{id}) — consulta o status.
 * Requer Bearer CIAM.
 */
export async function getPurchaseStatus(
  correlationId: string
): Promise<FunctionV2Result<PurchaseStatusV2>> {
  const token = await getV2AccessToken();
  if (!token) {
    return { error: 'Sem sessão CIAM (token ausente para consulta de status).' };
  }

  try {
    const response = await fetch(
      `${normalizeBase()}/purchase/${encodeURIComponent(correlationId)}`,
      { headers: { Authorization: `Bearer ${token}` } }
    );

    if (!response.ok) {
      return {
        error: `Falha ao consultar o status (HTTP ${response.status}).`,
        httpStatus: response.status,
      };
    }

    const data = (await response.json()) as PurchaseStatusV2;
    return { data, httpStatus: response.status };
  } catch (error) {
    console.error('Function v2 (status) error:', error);
    return { error: 'Erro de conexão com o gateway v2.' };
  }
}

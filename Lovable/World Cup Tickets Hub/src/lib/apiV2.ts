// =============================================================================
// Story 2.3 / F3 — Cliente da API v2 (gateway YARP) com Bearer token Entra.
//
// Paralelo ao src/lib/api.ts (v1, intocado). Toda chamada v2 inclui
// `Authorization: Bearer <accessToken>` obtido via MSAL acquireTokenSilent
// (lib/authV2.ts). O gateway YARP valida o JWT (AddJwtBearer) e propaga o claim
// `oid` como X-Entra-OID para a Function F1 (ADE-005 Inv 4).
//
// Base URL via VITE_GATEWAY_V2_URL (Container App do gateway). Nunca hardcoded.
// =============================================================================

import { getV2AccessToken } from '@/lib/authV2';

const GATEWAY_V2_URL = import.meta.env.VITE_GATEWAY_V2_URL ?? '';

export interface PurchaseV2Request {
  matchId: number;
  category: 'VIP' | 'Cat1' | 'Cat2';
  userId: number;
  quantity: number;
}

export interface PurchaseV2Accepted {
  correlationId: string;
  status: string;
}

export interface PurchaseV2Result {
  data?: PurchaseV2Accepted;
  error?: string;
  /** HTTP status (útil para o lab didático de cenários 401 — AC-12). */
  status?: number;
}

export interface MeV2Result {
  /** users.id (int) resolvido/vinculado/provisionado pelo /api/v2/me. */
  userId?: number;
  error?: string;
  /** HTTP status (401 sem token CIAM; 409 email já vinculado; 422 claims insuficientes). */
  status?: number;
}

/**
 * Story 3.5 (ADE-007 Inv 8) — GET /me no gateway v2 com Bearer token CIAM. O gateway
 * (fence CiamOnly) resolve/vincula/cria a linha `users` do cliente autenticado no Entra
 * External ID e devolve o `userId` (int) que o checkout v2 exige. É a unificação base
 * v1 ↔ CIAM: um cliente nato-CIAM (sem conta v1) vira cidadão de primeira classe na base.
 *
 * Fail-soft: sem gateway configurado (VITE_GATEWAY_V2_URL vazio — ex.: fluxo Oitavas via
 * Function direta) ou sem token CIAM, retorna { error } SEM userId → o caller cai no
 * fallback do id v1 (não quebra os fluxos que não passam pelo gateway CIAM).
 */
export async function meV2(): Promise<MeV2Result> {
  if (!GATEWAY_V2_URL) {
    return { error: 'Gateway v2 não configurado (VITE_GATEWAY_V2_URL ausente).' };
  }

  const token = await getV2AccessToken();
  if (!token) {
    return { error: 'Sem sessão CIAM (faça o "Login v2" Entra).' };
  }

  try {
    const response = await fetch(`${GATEWAY_V2_URL}/me`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      return { error: `Erro em /me v2 (${response.status})`, status: response.status };
    }

    const data = (await response.json()) as { userId: number };
    return { userId: data.userId, status: response.status };
  } catch (error) {
    console.error('API v2 /me Error:', error);
    return { error: 'Erro de conexão com o gateway v2 (/me).' };
  }
}

/**
 * AC-5 — POST /purchase no gateway v2 com Bearer token Entra.
 * Sem token (usuário não fez "Login v2") → erro local antes da chamada.
 * Gateway rejeita token inválido/expirado/aud errado com 401 (AC-12).
 */
export async function purchaseV2(body: PurchaseV2Request): Promise<PurchaseV2Result> {
  const token = await getV2AccessToken();
  if (!token) {
    return { error: 'Faça o "Login v2" (Entra) antes de comprar pelo fluxo v2.' };
  }

  try {
    const response = await fetch(`${GATEWAY_V2_URL}/purchase`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      // 401 esperado nos cenários de rejeição (token expirado/issuer/aud) — AC-12.
      let message = `Erro na requisição v2 (${response.status})`;
      if (response.status === 401) {
        message = 'Não autorizado (401): token Entra ausente, expirado ou inválido.';
      }
      return { error: message, status: response.status };
    }

    const data = (await response.json()) as PurchaseV2Accepted;
    return { data, status: response.status };
  } catch (error) {
    console.error('API v2 Error:', error);
    return { error: 'Erro de conexão com o gateway v2.' };
  }
}

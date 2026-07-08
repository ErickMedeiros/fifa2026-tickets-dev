// Story 4.3 (EPIC-004) AC-5/AC-6/AC-7 — primeira suíte de testes do backend Node v1.
// Cobre o `gatewayTrust.js`, o coração da confiança "admin 100% workforce" (Quartas): o
// gateway prova ao backend, via X-Gateway-Key == GATEWAY_SHARED_SECRET, que a request passou
// pelo guardião. Antes desta story esse mecanismo tinha ZERO cobertura automatizada.
//
// Mockamos o par legado (`./auth`) para provar de forma determinística o caminho de
// FALLBACK: quando o gateway NÃO é confiado, a request cai em authMiddleware → adminMiddleware
// (fluxo v1 intocado), nunca "meio-autenticada".

jest.mock('../auth', () => ({
  authMiddleware: jest.fn(),
  adminMiddleware: jest.fn(),
}));

const { authMiddleware, adminMiddleware } = require('../auth');
const { gatewayTrustMiddleware, safeEqual } = require('../gatewayTrust');

/** req/res/next mínimos. `req.header(name)` espelha o Express (lookup por nome exato aqui). */
function buildContext(headers = {}) {
  const req = { header: (name) => headers[name] };
  const res = {
    statusCode: null,
    status: jest.fn(function status(code) {
      this.statusCode = code;
      return this;
    }),
    json: jest.fn(),
  };
  const next = jest.fn();
  return { req, res, next };
}

describe('safeEqual', () => {
  // NOTA (AC-7): não medimos tempo-constante empiricamente — microbenchmark de timing em
  // unit test é frágil/não-determinístico. A garantia de tempo-constante vem de usar
  // crypto.timingSafeEqual (API nativa, já correta no código). Aqui provamos apenas o
  // COMPORTAMENTO FUNCIONAL: retorna false com segurança (sem lançar) fora do caso feliz.

  it('retorna true para strings idênticas', () => {
    expect(safeEqual('shared-secret', 'shared-secret')).toBe(true);
  });

  it('retorna false para strings de mesmo tamanho e conteúdo diferente', () => {
    expect(safeEqual('shared-secret', 'shured-secret')).toBe(false);
  });

  it('retorna false (sem lançar) para tamanhos diferentes', () => {
    expect(safeEqual('abc', 'abcd')).toBe(false);
  });

  it('retorna false quando um dos lados não é string', () => {
    expect(safeEqual(undefined, 'abc')).toBe(false);
    expect(safeEqual('abc', undefined)).toBe(false);
    expect(safeEqual(null, null)).toBe(false);
    expect(safeEqual(123, '123')).toBe(false);
    expect(safeEqual({}, {})).toBe(false);
  });

  it('retorna false para string vazia vs não-vazia', () => {
    expect(safeEqual('', 'abc')).toBe(false);
  });
});

describe('gatewayTrustMiddleware', () => {
  const ORIGINAL_SECRET = process.env.GATEWAY_SHARED_SECRET;

  afterEach(() => {
    jest.clearAllMocks();
    if (ORIGINAL_SECRET === undefined) {
      delete process.env.GATEWAY_SHARED_SECRET;
    } else {
      process.env.GATEWAY_SHARED_SECRET = ORIGINAL_SECRET;
    }
  });

  it('secret configurado + X-Gateway-Key correto → confia (req.user admin) e chama next(), sem tocar o fluxo legado', () => {
    process.env.GATEWAY_SHARED_SECRET = 'the-shared-secret';
    const { req, res, next } = buildContext({
      'X-Gateway-Key': 'the-shared-secret',
      'X-Entra-OID': 'oid-123',
    });

    gatewayTrustMiddleware(req, res, next);

    expect(req.user).toEqual({ role: 'admin', source: 'gateway', entra_oid: 'oid-123' });
    expect(next).toHaveBeenCalledTimes(1);
    expect(authMiddleware).not.toHaveBeenCalled();
    expect(adminMiddleware).not.toHaveBeenCalled();
  });

  it('confia mesmo sem X-Entra-OID (entra_oid vira null), pois o que prova a origem é o segredo', () => {
    process.env.GATEWAY_SHARED_SECRET = 'the-shared-secret';
    const { req, res, next } = buildContext({ 'X-Gateway-Key': 'the-shared-secret' });

    gatewayTrustMiddleware(req, res, next);

    expect(req.user).toEqual({ role: 'admin', source: 'gateway', entra_oid: null });
    expect(next).toHaveBeenCalledTimes(1);
  });

  it('secret VAZIO → NUNCA confia por header; cai no fluxo legado independente do X-Gateway-Key recebido', () => {
    process.env.GATEWAY_SHARED_SECRET = '';
    const { req, res, next } = buildContext({ 'X-Gateway-Key': 'anything-goes' });

    gatewayTrustMiddleware(req, res, next);

    expect(req.user).toBeUndefined();
    expect(authMiddleware).toHaveBeenCalledTimes(1);
    // O gateway não é confiado → next() NÃO é chamado direto (o par legado decide 401/403).
    expect(next).not.toHaveBeenCalled();
  });

  it('secret AUSENTE (env indefinida) → fluxo legado (fail-safe seguro, comportamento v1-only)', () => {
    delete process.env.GATEWAY_SHARED_SECRET;
    const { req, res, next } = buildContext({ 'X-Gateway-Key': 'whatever' });

    gatewayTrustMiddleware(req, res, next);

    expect(req.user).toBeUndefined();
    expect(authMiddleware).toHaveBeenCalledTimes(1);
  });

  it('secret configurado + header AUSENTE → fluxo legado (nunca meio-autenticado)', () => {
    process.env.GATEWAY_SHARED_SECRET = 'the-shared-secret';
    const { req, res, next } = buildContext({});

    gatewayTrustMiddleware(req, res, next);

    expect(req.user).toBeUndefined();
    expect(authMiddleware).toHaveBeenCalledTimes(1);
  });

  it('secret configurado + header INCORRETO → fluxo legado', () => {
    process.env.GATEWAY_SHARED_SECRET = 'the-shared-secret';
    const { req, res, next } = buildContext({ 'X-Gateway-Key': 'wrong-secret' });

    gatewayTrustMiddleware(req, res, next);

    expect(req.user).toBeUndefined();
    expect(authMiddleware).toHaveBeenCalledTimes(1);
  });

  it('no fallback, encadeia adminMiddleware quando authMiddleware autentica com sucesso', () => {
    process.env.GATEWAY_SHARED_SECRET = 'the-shared-secret';
    // authMiddleware "bem-sucedido" invoca o callback recebido (que chama adminMiddleware).
    authMiddleware.mockImplementation((req, res, cb) => cb());
    const { req, res, next } = buildContext({}); // sem X-Gateway-Key → fallback

    gatewayTrustMiddleware(req, res, next);

    expect(authMiddleware).toHaveBeenCalledTimes(1);
    expect(adminMiddleware).toHaveBeenCalledTimes(1);
  });
});

using System.Security.Cryptography;
using System.Text;

namespace Fifa2026.V2.Functions.Infrastructure;

/// <summary>
/// Resultado da avaliação de confiança serviço-a-serviço (ADE-009 Inv 1).
/// </summary>
internal enum GatewayTrustDecision
{
    /// <summary>Segredo vazio/ausente — trava DESLIGADA (comportamento legado). Preserva os
    /// labs SEM gateway (Oitavas/F1, dev local): a Function segue como <c>Anonymous</c>.</summary>
    LegacyBypass,

    /// <summary>Segredo configurado E header bate (comparação timing-safe) — request confiada.</summary>
    Trusted,

    /// <summary>Segredo configurado E header ausente/divergente — REJEITADA (fail-closed → 401).</summary>
    Rejected,
}

/// <summary>
/// Story 4.2 (ADE-009 Inv 1) — generalização .NET do <c>gatewayTrust.js</c> das Quartas
/// (<c>fifa2026-api/src/middleware/gatewayTrust.js</c>). Decide se uma request "provou" ter
/// vindo do gateway comparando o header <c>X-Gateway-Key</c> com o segredo configurado em
/// TEMPO CONSTANTE (anti timing-attack), com a MESMA semântica do lado Node:
///   - segredo vazio/ausente → <see cref="GatewayTrustDecision.LegacyBypass"/> (trava desligada);
///   - segredo configurado + header igual → <see cref="GatewayTrustDecision.Trusted"/>;
///   - segredo configurado + header ausente/divergente → <see cref="GatewayTrustDecision.Rejected"/>.
///
/// Lógica PURA (sem I/O) para ser exaustivamente testável — o middleware é só o adaptador.
/// O gateway continua o guardião ÚNICO do JWT (ADE-004 preservada): isto NÃO valida token,
/// só prova a ORIGEM (que o gateway chamou).
/// </summary>
internal static class GatewayKeyValidator
{
    /// <summary>Header injetado pelo gateway YARP (mesmo nome do <c>gatewayTrust.js</c>).</summary>
    internal const string HeaderName = "X-Gateway-Key";

    /// <summary>Chave de configuração/App Setting do segredo (KV reference resolvida pela MI
    /// em runtime — Story 4.1). MESMO nome do lado Node (singular, sem sufixo por serviço —
    /// AC-4: um único segredo compartilhado entre os clusters).</summary>
    internal const string SecretConfigKey = "GATEWAY_SHARED_SECRET";

    /// <summary>
    /// Avalia a confiança. <paramref name="configuredSecret"/> é o valor de config do serviço;
    /// <paramref name="incomingKey"/> é o header <c>X-Gateway-Key</c> recebido (pode ser null).
    /// </summary>
    internal static GatewayTrustDecision Evaluate(string? configuredSecret, string? incomingKey)
    {
        // Fail-open condicionado ao segredo: vazio = trava desligada = legado (labs sem gateway).
        if (string.IsNullOrEmpty(configuredSecret))
        {
            return GatewayTrustDecision.LegacyBypass;
        }

        // Segredo configurado = trava ARMADA (fail-closed). Só confia se o header bate.
        return IsTimingSafeEqual(incomingKey, configuredSecret)
            ? GatewayTrustDecision.Trusted
            : GatewayTrustDecision.Rejected;
    }

    /// <summary>
    /// Comparação em tempo constante (equivalente ao <c>crypto.timingSafeEqual</c> do Node).
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> retorna false para tamanhos
    /// diferentes SEM lançar (ao contrário do Node, que exige buffers do mesmo tamanho — por
    /// isso o <c>gatewayTrust.js</c> checa o length antes; aqui a API já cobre esse caso).
    /// </summary>
    private static bool IsTimingSafeEqual(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}

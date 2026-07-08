using Fifa2026.V2.Functions.Infrastructure;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// Story 4.2 (ADE-009 Inv 1) — cobertura exaustiva da lógica timing-safe/gating que fecha o
/// P0 do bypass das Functions. Espelha a semântica do <c>gatewayTrust.js</c>: segredo vazio =
/// legado (passa); configurado = fail-closed (header deve bater em tempo constante).
/// </summary>
public sealed class GatewayKeyValidatorTests
{
    private const string Secret = "s3cr3t-shared-key-123";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptySecret_IsLegacyBypass_RegardlessOfHeader(string? secret)
    {
        // Trava DESLIGADA: preserva os labs sem gateway (Oitavas/F1) — o header é irrelevante.
        Assert.Equal(GatewayTrustDecision.LegacyBypass, GatewayKeyValidator.Evaluate(secret, null));
        Assert.Equal(GatewayTrustDecision.LegacyBypass, GatewayKeyValidator.Evaluate(secret, ""));
        Assert.Equal(GatewayTrustDecision.LegacyBypass, GatewayKeyValidator.Evaluate(secret, "anything"));
        Assert.Equal(GatewayTrustDecision.LegacyBypass, GatewayKeyValidator.Evaluate(secret, Secret));
    }

    [Fact]
    public void ConfiguredSecret_MatchingHeader_IsTrusted()
    {
        Assert.Equal(GatewayTrustDecision.Trusted, GatewayKeyValidator.Evaluate(Secret, Secret));
    }

    [Fact]
    public void ConfiguredSecret_NullHeader_IsRejected()
    {
        // Header ausente (null) com segredo armado → fail-closed.
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(Secret, null));
    }

    [Fact]
    public void ConfiguredSecret_EmptyHeader_IsRejected()
    {
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(Secret, ""));
    }

    [Theory]
    [InlineData("wrong-key")]                 // valor totalmente diferente
    [InlineData("s3cr3t-shared-key-124")]     // difere por 1 caractere (MESMO tamanho)
    [InlineData("s3cr3t-shared-key-1234567")] // tamanho diferente (mais longo)
    [InlineData("s3cr3t")]                     // prefixo (tamanho diferente, menor)
    [InlineData("S3CR3T-SHARED-KEY-123")]     // case diferente (segredo é case-sensitive)
    public void ConfiguredSecret_DivergentHeader_IsRejected(string header)
    {
        // Cobre o "tamanhos diferentes" que o CryptographicOperations.FixedTimeEquals trata
        // sem lançar (ao contrário do crypto.timingSafeEqual do Node).
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(Secret, header));
    }

    [Fact]
    public void WhitespaceSecret_IsConsideredConfigured_AndFailsClosed()
    {
        // string.IsNullOrEmpty(" ") == false → " " é um segredo "configurado" (trava armada).
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(" ", null));
        Assert.Equal(GatewayTrustDecision.Trusted, GatewayKeyValidator.Evaluate(" ", " "));
    }
}

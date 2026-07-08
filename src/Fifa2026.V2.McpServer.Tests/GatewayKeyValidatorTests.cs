using Fifa2026.V2.McpServer.Infrastructure;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// Story 4.2 (ADE-009 Inv 1) — cobertura exaustiva da lógica timing-safe/gating do McpServer
/// (cópia por serviço da mesma decisão do lado Functions). Segredo vazio = legado (passa);
/// configurado = fail-closed (header deve bater em tempo constante).
/// </summary>
public sealed class GatewayKeyValidatorTests
{
    private const string Secret = "s3cr3t-shared-key-123";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptySecret_IsLegacyBypass_RegardlessOfHeader(string? secret)
    {
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
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(Secret, null));
    }

    [Fact]
    public void ConfiguredSecret_EmptyHeader_IsRejected()
    {
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(Secret, ""));
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("s3cr3t-shared-key-124")]     // 1 char de diferença (mesmo tamanho)
    [InlineData("s3cr3t-shared-key-1234567")] // tamanho diferente (mais longo)
    [InlineData("s3cr3t")]                     // prefixo (menor)
    [InlineData("S3CR3T-SHARED-KEY-123")]     // case-sensitive
    public void ConfiguredSecret_DivergentHeader_IsRejected(string header)
    {
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(Secret, header));
    }

    [Fact]
    public void WhitespaceSecret_IsConsideredConfigured_AndFailsClosed()
    {
        Assert.Equal(GatewayTrustDecision.Rejected, GatewayKeyValidator.Evaluate(" ", null));
        Assert.Equal(GatewayTrustDecision.Trusted, GatewayKeyValidator.Evaluate(" ", " "));
    }
}

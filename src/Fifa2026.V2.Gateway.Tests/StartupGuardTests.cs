using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 4.3 (EPIC-004) AC-8 — guards fail-closed de startup do gateway. O
/// <c>Gateway/Program.cs</c> LANÇA <see cref="InvalidOperationException"/> quando qualquer um
/// dos quatro IDs de identidade (<c>Jwt:CiamTenantId</c>/<c>CiamClientId</c>/
/// <c>AdminTenantId</c>/<c>AdminClientId</c>) está ausente ou o tenant é <c>"common"</c>
/// (aceitaria tokens de QUALQUER tenant). Hoje NENHUM teste cobre esses guards — uma
/// regressão que reintroduzisse <c>"common"</c> ou removesse a checagem passaria
/// silenciosamente (CARRY-FORWARD M-1 do gate S2.2 / ADE-007 Inv 1/5).
///
/// Estratégia: sobe o host com <see cref="WebApplicationFactory{TEntryPoint}"/> (mesmo padrão
/// de <see cref="GatewayTestFixture"/>) a partir de um baseline VÁLIDO e quebra EXATAMENTE
/// uma chave por teste — assim a falha é atribuível ao guard sob teste, não a outra config.
/// </summary>
public sealed class StartupGuardTests
{
    /// <summary>
    /// Factory com baseline de identidade VÁLIDO (GUIDs de teste, nunca <c>"common"</c>) e um
    /// conjunto de overrides que quebram uma chave. Os valores de override são <c>""</c>
    /// (simula ausência — o guard usa <c>string.IsNullOrWhiteSpace</c>) ou <c>"common"</c>.
    /// </summary>
    private sealed class GuardWebFactory : WebApplicationFactory<Program>
    {
        private readonly IReadOnlyDictionary<string, string> _overrides;

        public GuardWebFactory(IReadOnlyDictionary<string, string> overrides) => _overrides = overrides;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Baseline válido (UseSetting precede appsettings.json — ver GatewayTestFixture).
            builder.UseSetting("Jwt:CiamTenantId", TestTokenFactory.CiamTenantId);
            builder.UseSetting("Jwt:CiamClientId", TestTokenFactory.CiamClientId);
            builder.UseSetting("Jwt:AdminTenantId", TestTokenFactory.AdminTenantId);
            builder.UseSetting("Jwt:AdminClientId", TestTokenFactory.AdminClientId);

            foreach (var (key, value) in _overrides)
            {
                builder.UseSetting(key, value);
            }
        }
    }

    private static Exception? CaptureStartup(IReadOnlyDictionary<string, string> overrides)
    {
        using var factory = new GuardWebFactory(overrides);
        // O guard roda ANTES de builder.Build(); CreateClient() força a construção do host e
        // propaga a falha de inicialização.
        return Record.Exception(() =>
        {
            using var client = factory.CreateClient();
        });
    }

    private static void AssertStartupFailsWith(string overrideKey, string overrideValue, string expectedFragment)
    {
        var exception = CaptureStartup(new Dictionary<string, string> { [overrideKey] = overrideValue });

        Assert.NotNull(exception);

        var chain = Flatten(exception!).ToList();
        // Fail-closed: a inicialização falhou por causa do guard específico (a mensagem cita a
        // chave de config exata — nada mais no pipeline menciona esse literal).
        Assert.Contains(chain, e => e.Message.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
        // E é um InvalidOperationException (o tipo que os guards lançam), não uma falha lateral.
        Assert.Contains(chain, e => e is InvalidOperationException);
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        var seen = new HashSet<Exception>();
        var stack = new Stack<Exception>();
        stack.Push(exception);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            yield return current;

            if (current.InnerException is not null)
            {
                stack.Push(current.InnerException);
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    stack.Push(inner);
                }
            }
        }
    }

    [Fact]
    public void Missing_CiamTenantId_fails_startup() =>
        AssertStartupFailsWith("Jwt:CiamTenantId", "", "Jwt:CiamTenantId");

    [Fact]
    public void Common_CiamTenantId_fails_startup() =>
        AssertStartupFailsWith("Jwt:CiamTenantId", "common", "Jwt:CiamTenantId");

    [Fact]
    public void Missing_CiamClientId_fails_startup() =>
        AssertStartupFailsWith("Jwt:CiamClientId", "", "Jwt:CiamClientId");

    [Fact]
    public void Missing_AdminTenantId_fails_startup() =>
        AssertStartupFailsWith("Jwt:AdminTenantId", "", "Jwt:AdminTenantId");

    [Fact]
    public void Common_AdminTenantId_fails_startup() =>
        AssertStartupFailsWith("Jwt:AdminTenantId", "common", "Jwt:AdminTenantId");

    [Fact]
    public void Missing_AdminClientId_fails_startup() =>
        AssertStartupFailsWith("Jwt:AdminClientId", "", "Jwt:AdminClientId");

    /// <summary>
    /// Anti-false-positive (CodeRabbit focus): com o baseline COMPLETO e válido o host sobe
    /// sem lançar — prova que as falhas acima vêm da config quebrada, não do próprio harness.
    /// </summary>
    [Fact]
    public void Valid_baseline_boots_without_throwing()
    {
        var exception = CaptureStartup(new Dictionary<string, string>());

        Assert.Null(exception);
    }
}

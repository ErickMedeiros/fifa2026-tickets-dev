using System.Text.Json;
using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Functions;
using Fifa2026.V2.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// AC-4/AC-6/AC-7 (F1) — comportamento do consumer: happy path, idempotência (duplicata
/// não falha), e falha permanente (categoria inexistente → re-throw → DLQ).
/// Story 3.1 (ADE-008 Inv 3) — a notificação pós-compra é INLINE (log estruturado, sem
/// n8n): dispara APENAS em Inserted, NUNCA em Duplicate/CategoryNotFound (ADE-000 Inv 4),
/// e por ser um log nunca lança nem propaga.
/// </summary>
public sealed class PurchaseConsumerFunctionTests
{
    private static string Serialize(PurchaseMessage message) => JsonSerializer.Serialize(message);

    private static PurchaseMessage NewMessage() => new()
    {
        CorrelationId = Guid.NewGuid(),
        MatchId = 1,
        Category = "VIP",
        UserId = 7,
        Quantity = 2
    };

    private static PurchaseConsumerFunction Build(IPurchaseRepository repo) =>
        new(repo, NullLogger<PurchaseConsumerFunction>.Instance);

    private static PurchaseConsumerFunction Build(IPurchaseRepository repo, ILogger<PurchaseConsumerFunction> logger) =>
        new(repo, logger);

    /// <summary>
    /// Story 3.1 — verifica a notificação pós-compra INLINE via captura do ILogger. Substitui
    /// o antigo <c>Mock&lt;IN8nWebhookNotifier&gt;.Verify(...)</c> (o notifier foi removido):
    /// afirma um log de nível Information cuja mensagem formatada casa com o predicado, usando
    /// o matcher canônico de <c>ILogger.Log&lt;TState&gt;</c> do Moq (It.IsAnyType).
    /// </summary>
    private static void VerifyInlineNotification(
        Mock<ILogger<PurchaseConsumerFunction>> logger,
        Func<string, bool> messagePredicate,
        Times times) =>
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => messagePredicate(v.ToString()!)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    [Fact]
    public async Task Happy_path_inserts_and_completes()
    {
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Inserted);

        var sut = Build(repo.Object);

        await sut.RunAsync(Serialize(NewMessage()), CancellationToken.None);

        repo.Verify(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Passes_EntraOid_Through_To_Repository()
    {
        // Story 2.3 AC-9 — o consumer repassa o entra_oid (claim oid do gateway) para
        // o repositório, que o grava na coluna entra_oid (verificado via captura do arg).
        var oid = Guid.Parse("55555555-6666-7777-8888-999999999999");
        PurchaseMessage? captured = null;

        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(InsertOutcome.Inserted);

        var sut = Build(repo.Object);

        var message = NewMessage();
        message.EntraOid = oid;
        await sut.RunAsync(Serialize(message), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(oid, captured!.EntraOid);
    }

    [Fact]
    public async Task Duplicate_is_swallowed_silently_no_throw()
    {
        // AC-6: enviar a mesma mensagem 2x → consumer NÃO lança (não vai para DLQ).
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Duplicate);

        var sut = Build(repo.Object);

        var exception = await Record.ExceptionAsync(() => sut.RunAsync(Serialize(NewMessage()), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CategoryNotFound_throws_to_route_to_dlq()
    {
        // AC-7: matchId/category inválidos → falha permanente → re-throw → DLQ.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.CategoryNotFound);

        var sut = Build(repo.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync(Serialize(NewMessage()), CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_json_throws_to_route_to_dlq()
    {
        var repo = new Mock<IPurchaseRepository>();
        var sut = Build(repo.Object);

        await Assert.ThrowsAsync<JsonException>(
            () => sut.RunAsync("{ not valid json", CancellationToken.None));

        repo.Verify(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Empty_correlationId_throws()
    {
        var repo = new Mock<IPurchaseRepository>();
        var sut = Build(repo.Object);

        var message = NewMessage();
        message.CorrelationId = Guid.Empty;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync(Serialize(message), CancellationToken.None));

        repo.Verify(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Story 3.1 (ADE-008 Inv 3) — notificação pós-compra INLINE (log, sem n8n)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Inline_notification_is_emitted_on_Inserted()
    {
        // ADE-008 Inv 3: após InsertOutcome.Inserted, o consumer emite a notificação
        // pós-compra inline (log estruturado) — substitui o antigo webhook n8n.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Inserted);

        var logger = new Mock<ILogger<PurchaseConsumerFunction>>();
        var sut = Build(repo.Object, logger.Object);

        await sut.RunAsync(Serialize(NewMessage()), CancellationToken.None);

        VerifyInlineNotification(logger, m => m.Contains("Notificação pós-compra"), Times.Once());
    }

    [Fact]
    public async Task Inline_notification_on_Inserted_carries_correlationId_from_body()
    {
        // AC-4: a notificação inline herda o correlationId do BeginScope já aberto — o
        // correlationId vem do CORPO da mensagem (não das Application Properties). Provamos
        // que a mensagem da notificação carrega exatamente esse correlationId.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Inserted);

        var logger = new Mock<ILogger<PurchaseConsumerFunction>>();
        var sut = Build(repo.Object, logger.Object);

        var message = NewMessage();
        await sut.RunAsync(Serialize(message), CancellationToken.None);

        VerifyInlineNotification(
            logger,
            m => m.Contains("Notificação pós-compra") && m.Contains(message.CorrelationId.ToString()),
            Times.Once());
    }

    [Fact]
    public async Task Inline_notification_is_NOT_emitted_on_Duplicate()
    {
        // ADE-000 Inv 4: idempotência preservada — em Duplicate (reentrega do Service Bus)
        // a notificação pós-compra NÃO é emitida.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Duplicate);

        var logger = new Mock<ILogger<PurchaseConsumerFunction>>();
        var sut = Build(repo.Object, logger.Object);

        await sut.RunAsync(Serialize(NewMessage()), CancellationToken.None);

        VerifyInlineNotification(logger, m => m.Contains("Notificação pós-compra"), Times.Never());
    }

    [Fact]
    public async Task Inline_notification_is_NOT_emitted_on_CategoryNotFound()
    {
        // CategoryNotFound → falha permanente (re-throw → DLQ) e NENHUMA notificação emitida.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.CategoryNotFound);

        var logger = new Mock<ILogger<PurchaseConsumerFunction>>();
        var sut = Build(repo.Object, logger.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync(Serialize(NewMessage()), CancellationToken.None));

        VerifyInlineNotification(logger, m => m.Contains("Notificação pós-compra"), Times.Never());
    }
}

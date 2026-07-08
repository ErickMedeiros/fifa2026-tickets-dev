using System.Text.Json;
using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Functions;

/// <summary>
/// AC-4/AC-6/AC-7 — Consumidor do fluxo de compra v2.
/// Service Bus trigger em `tickets-purchase` → INSERT idempotente em `purchases`
/// (source='v2', status='completed') via UNIQUE constraint + catch SqlException 2627.
///
/// Tratamento de falhas (AC-7 / DLQ):
///  - Duplicata (2627) → ignorada silenciosamente (idempotência atingida).
///  - matchId/category inválidos → falha PERMANENTE: re-throw para que, após
///    maxDeliveryCount (10), a mensagem caia automaticamente no DLQ.
///  - Erros transitórios (timeout SQL etc.) → re-throw → reentrega → eventual DLQ.
/// </summary>
public sealed class PurchaseConsumerFunction
{
    private readonly IPurchaseRepository _repository;
    private readonly ILogger<PurchaseConsumerFunction> _logger;

    public PurchaseConsumerFunction(
        IPurchaseRepository repository,
        ILogger<PurchaseConsumerFunction> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function(nameof(PurchaseConsumerFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger("tickets-purchase", Connection = "ServiceBusConnection")] string messageBody,
        CancellationToken cancellationToken)
    {
        PurchaseMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<PurchaseMessage>(
                messageBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            // Mensagem malformada não tem como ser reprocessada com sucesso → DLQ.
            _logger.LogError(ex, "Mensagem com JSON inválido em tickets-purchase. Será encaminhada ao DLQ.");
            throw;
        }

        if (message is null || message.CorrelationId == Guid.Empty)
        {
            _logger.LogError("Mensagem sem correlationId válido. Será encaminhada ao DLQ.");
            throw new InvalidOperationException("Mensagem inválida: correlationId ausente.");
        }

        // BeginScope propaga o correlationId para o App Insights (ADE-000 Inv 5 — log hop).
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId }))
        {
            _logger.LogInformation(
                "Processando compra v2: matchId={MatchId} category={Category} userId={UserId} quantity={Quantity}",
                message.MatchId, message.Category, message.UserId, message.Quantity);

            var outcome = await _repository.InsertPurchaseAsync(message, cancellationToken);

            switch (outcome)
            {
                case InsertOutcome.Inserted:
                    _logger.LogInformation("Compra v2 gravada com sucesso (correlationId={CorrelationId}).", message.CorrelationId);

                    // ADE-008 Inv 3 — notificação pós-compra INLINE (sem orquestração externa/n8n):
                    // um log estruturado, correlacionado pelo BeginScope já aberto acima (ADE-000
                    // Inv 5). É o trace que o motor do F6 mapeia para o nó Consumer — a notificação
                    // fica "dobrada" nesse hop (F6 = 5 nós). Dispara APENAS em Inserted (nunca em
                    // Duplicate/CategoryNotFound → ADE-000 Inv 4: uma reentrega do Service Bus não
                    // re-notifica). O payload sai do CORPO da mensagem (correlationId), não das
                    // Application Properties do Service Bus. Um log nunca lança — a defesa em
                    // profundidade do try/catch anterior (webhook de rede) deixa de ser necessária.
                    _logger.LogInformation(
                        "Notificação pós-compra enviada (inline, sem orquestração externa): " +
                        "matchId={MatchId} category={Category} (correlationId={CorrelationId}).",
                        message.MatchId, message.Category, message.CorrelationId);
                    break;

                case InsertOutcome.Duplicate:
                    // Idempotência: mensagem reentregue. Completa sem erro (não vai para DLQ).
                    _logger.LogInformation("Compra v2 já existente — duplicata ignorada (correlationId={CorrelationId}).", message.CorrelationId);
                    break;

                case InsertOutcome.CategoryNotFound:
                    // Falha permanente: re-throw força reentrega → DLQ após maxDeliveryCount (AC-7).
                    _logger.LogError(
                        "Categoria inexistente para matchId={MatchId} category={Category}. Encaminhando ao DLQ.",
                        message.MatchId, message.Category);
                    throw new InvalidOperationException(
                        $"Nenhuma ticket_category para matchId={message.MatchId}, category={message.Category}.");
            }
        }
    }
}

using Fifa2026.V2.Functions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Functions;

/// <summary>
/// Story 3.5 (ADE-007 v1.2/v1.3 Invariante 8) — <c>GET /api/v2/me</c>: o UNIFICADOR base
/// v1 ↔ CIAM. Provisiona <i>just-in-time</i> (ou resolve/vincula) uma linha <c>users</c>
/// para o cliente autenticado no Microsoft Entra External ID (CIAM), de forma que um
/// nato-CIAM — que nunca teve conta v1 — consiga completar uma compra v2 (hoje
/// <c>PurchaseRequest.UserId</c> é <c>int</c> obrigatório <c>[Range(1, int.MaxValue)]</c>,
/// exigindo uma linha <c>users</c> pré-existente).
///
/// Contrato <b>resolve-or-provision</b> com precedência determinística (Invariante 8):
///   1. <b>resolve por oid</b> — cliente já unificado (migrado por Inv 6 ou já JIT). Zero write.
///   2. <b>link por email</b> — usuário da BASE v1 (bcrypt) chegando via CIAM: vincula o
///      <c>entra_oid</c> à linha existente (bcrypt preservado). É a Inv 6 on-demand.
///   3. <b>insert</b> — nato-CIAM genuíno: nova linha com sentinela fail-closed no password.
///
/// A Function NÃO valida o JWT (ADE-004 / Inv 7 — só o gateway o faz); confia no
/// <c>X-Entra-OID</c> propagado pelo gateway (guardião único), exatamente como
/// <c>PurchaseEntryFunction</c>. O fence <c>CiamOnly</c> no gateway (AC-3) garante que só
/// um token CIAM (nunca um admin/workforce) alcança este endpoint — um <c>oid</c> de admin
/// jamais provisiona/vincula uma linha de cliente.
///
/// authLevel Anonymous: a segurança de borda é do gateway (JWT + fence) e do
/// <c>GatewayKeyValidationMiddleware</c> (X-Gateway-Key, Story 4.2 — functions-f1 é cluster
/// confiável). A URL real da Function nunca é exposta (ADE-004 Inv 1/5).
/// </summary>
public sealed class MeFunction
{
    /// <summary>Header de identidade propagado pelo gateway (claim <c>oid</c> do JWT CIAM).</summary>
    private const string EntraOidHeader = "X-Entra-OID";

    /// <summary>
    /// Claims <c>email</c>/<c>name</c> do token CIAM, propagados aditivamente pelo gateway
    /// (ADE-007 v1.2 Inv 8 — mesmo padrão de <c>X-Entra-OID</c>; a Function nunca lê o token).
    /// Usados só no arm de INSERT (colunas <c>NOT NULL</c>). São PII — nunca logados (Inv 8 (c)).
    /// </summary>
    private const string EntraEmailHeader = "X-Entra-Email";
    private const string EntraNameHeader = "X-Entra-Name";

    private readonly IUserRepository _users;
    private readonly ILogger<MeFunction> _logger;

    public MeFunction(IUserRepository users, ILogger<MeFunction> logger)
    {
        _users = users;
        _logger = logger;
    }

    [Function(nameof(MeFunction))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v2/me")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // Identidade validada pelo gateway e propagada como X-Entra-OID. Ausente/inválida →
        // 401 (jamais provisiona sem identidade validada — Invariante 8, flag de segurança
        // (a); mesmo trust model de PurchaseEntryFunction). NÃO logamos o oid (PII).
        var oidHeader = req.Headers[EntraOidHeader].ToString();
        if (string.IsNullOrWhiteSpace(oidHeader) || !Guid.TryParse(oidHeader, out var entraOid))
        {
            _logger.LogWarning("GET /api/v2/me sem X-Entra-OID válido — 401 (identidade não propagada).");
            return new UnauthorizedObjectResult(new { error = "Identidade Entra ausente ou inválida." });
        }

        var email = NullIfBlank(req.Headers[EntraEmailHeader].ToString());
        var name = NullIfBlank(req.Headers[EntraNameHeader].ToString());

        var result = await ResolveOrProvisionAsync(entraOid, email, name, cancellationToken);

        return result.Outcome switch
        {
            MeOutcome.Resolved or MeOutcome.Linked or MeOutcome.Provisioned =>
                new OkObjectResult(new { userId = result.UserId }),

            // Email já pertence a OUTRA identidade CIAM (flag de segurança (b) da Inv 8):
            // nunca devolvemos o id de uma linha de outra identidade nem sobrescrevemos.
            MeOutcome.Conflict =>
                new ConflictObjectResult(new { error = "E-mail já vinculado a outra identidade." }),

            // Sem email/name propagados não há como criar a linha (colunas NOT NULL). A
            // disponibilidade dos claims no user flow do tenant é "a confirmar com owner".
            MeOutcome.InsufficientClaims =>
                new ObjectResult(new { error = "Claims insuficientes (email/name) para provisionar o usuário." })
                { StatusCode = StatusCodes.Status422UnprocessableEntity },

            _ =>
                new ObjectResult(new { error = "Erro ao resolver a identidade." })
                { StatusCode = StatusCodes.Status500InternalServerError },
        };
    }

    /// <summary>
    /// A precedência determinística oid → email-link → insert (Invariante 8) com idempotência
    /// sob concorrência: todo WRITE é guardado pelos índices UNIQUE (<c>UQ_users_entra_oid</c>
    /// + <c>UQ_users_email</c>); uma duplicata devolve <c>null</c> da primitiva (não lança) e
    /// AQUI re-resolvemos — nunca "SELECT-then-INSERT" como única defesa (ADE-000 Inv 4).
    /// </summary>
    private async Task<MeResult> ResolveOrProvisionAsync(
        Guid entraOid, string? email, string? name, CancellationToken cancellationToken)
    {
        // (1) resolve por oid — cliente já unificado. Zero write.
        var byOid = await _users.FindIdByEntraOidAsync(entraOid, cancellationToken);
        if (byOid is int resolvedId)
        {
            return new MeResult(MeOutcome.Resolved, resolvedId);
        }

        // (2) link por email — usuário da BASE v1 chegando via CIAM: vincula (não insere).
        if (!string.IsNullOrWhiteSpace(email))
        {
            var linkedId = await _users.TryLinkByEmailAsync(entraOid, email, cancellationToken);
            if (linkedId is int linked)
            {
                return new MeResult(MeOutcome.Linked, linked);
            }
        }

        // (3) provisionar exige email + name (colunas NOT NULL). Ausentes → não fabricamos.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name))
        {
            return new MeResult(MeOutcome.InsufficientClaims, null);
        }

        // (3) insert — nato-CIAM genuíno. Guardado pelos índices UNIQUE.
        var insertedId = await _users.TryInsertCiamUserAsync(entraOid, email, name, cancellationToken);
        if (insertedId is int inserted)
        {
            return new MeResult(MeOutcome.Provisioned, inserted);
        }

        // (3b) corrida (2627/2601): a primitiva devolveu null → re-resolve autoritativo.
        //      Primeiro por oid (a MINHA identidade é a âncora), depois por email.
        var reByOid = await _users.FindIdByEntraOidAsync(entraOid, cancellationToken);
        if (reByOid is int wonByOid)
        {
            return new MeResult(MeOutcome.Resolved, wonByOid);
        }

        var existing = await _users.FindByEmailAsync(email, cancellationToken);
        if (existing is { } row)
        {
            // Vinculado concorrentemente a NÓS (mesmo oid) → resolve; a OUTRA identidade → conflito.
            return row.EntraOid == entraOid
                ? new MeResult(MeOutcome.Resolved, row.Id)
                : new MeResult(MeOutcome.Conflict, null);
        }

        // Não deveria acontecer: duplicata reportada mas nenhuma linha no re-resolve.
        throw new InvalidOperationException(
            "resolve-or-provision: duplicata reportada mas nenhuma linha encontrada no re-resolve.");
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>O passo da precedência (Invariante 8) que resolveu a request.</summary>
    private enum MeOutcome
    {
        /// <summary>Passo 1 — cliente já unificado (resolve por oid).</summary>
        Resolved,
        /// <summary>Passo 2 — usuário da base v1 vinculado on-demand (link por email).</summary>
        Linked,
        /// <summary>Passo 3 — nato-CIAM provisionado (insert).</summary>
        Provisioned,
        /// <summary>Email pertence a outra identidade CIAM (flag de segurança (b)).</summary>
        Conflict,
        /// <summary>Sem email/name para provisionar uma linha nova (colunas NOT NULL).</summary>
        InsufficientClaims,
    }

    private readonly record struct MeResult(MeOutcome Outcome, int? UserId);
}

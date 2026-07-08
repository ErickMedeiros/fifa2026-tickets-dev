namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Story 3.5 (ADE-007 v1.2 Invariante 8) — acesso a dados sobre a tabela <c>users</c>
/// (mesma DB do v1) para o contrato <b>resolve-or-provision</b> do endpoint
/// <c>GET /api/v2/me</c> (unificação base v1 ↔ CIAM).
///
/// Exposto como quatro PRIMITIVAS finas (uma statement SQL cada), deliberadamente sem a
/// orquestração de precedência: a precedência determinística <c>oid → email-link → insert</c>
/// (Invariante 8) e a idempotência sob concorrência vivem em <see cref="Fifa2026.V2.Functions.Functions.MeFunction"/>,
/// que depende desta interface e pode ser testado com um mock (mesmo padrão de
/// <see cref="IPurchaseRepository"/> mockado em <c>PurchaseConsumerFunctionTests</c>). As
/// primitivas que tocam SQL (esta impl. concreta, <see cref="UserRepository"/>) são cobertas
/// por teste de integração fora do projeto unitário — idêntico ao <see cref="PurchaseRepository"/>.
///
/// A duplicata de INSERT (corrida entre dois primeiros-logins do mesmo oid/email) NÃO
/// vaza <c>SqlException</c> para a orquestração: <see cref="TryInsertAsync"/> captura
/// <c>2627/2601</c> e devolve <c>null</c> (ADE-000 Inv 4 — mesmo tratamento de
/// <c>InsertOutcome.Duplicate</c> em <see cref="PurchaseRepository"/>), permitindo que a
/// orquestração seja testada de forma determinística sem um banco real.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Passo 1 (resolve por oid) — <c>SELECT id FROM dbo.users WHERE entra_oid = @oid</c>.
    /// Retorna o <c>id</c> existente (cliente já unificado — migrado por Inv 6 ou já
    /// JIT-provisionado) ou <c>null</c> se nenhuma linha corresponde ao oid.
    /// </summary>
    Task<int?> FindIdByEntraOidAsync(Guid entraOid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Passo 2 (link por email) — <c>UPDATE dbo.users SET entra_oid=@oid WHERE email=@email
    /// AND entra_oid IS NULL</c>. É o UPDATE idempotente da migração de cadastro
    /// (<c>migration-v1-ciam-design.md §4</c> / Inv 6) executado on-demand: o usuário da
    /// BASE v1 (bcrypt) chegando via CIAM vira uma linha unificada. Retorna o <c>id</c>
    /// vinculado (1 linha afetada) ou <c>null</c> se não havia linha vinculável por esse
    /// email (email inexistente OU já vinculado a outro oid — a distinção é resolvida no
    /// re-resolve pós-duplicata).
    /// </summary>
    Task<int?> TryLinkByEmailAsync(Guid entraOid, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Passo 3 (provision) — <c>INSERT</c> de uma nova linha <c>users</c> nato-CIAM com
    /// <c>entra_oid=@oid</c>, <c>name</c>/<c>email</c> dos claims propagados pelo gateway,
    /// <c>role</c> no <c>DEFAULT ('user')</c> e <c>password</c> = sentinela fail-closed
    /// (<see cref="UserRepository.CiamManagedPasswordSentinel"/>, ADE-007 Inv 8.1).
    /// Guardado por <c>UQ_users_entra_oid</c> E <c>UQ_users_email</c>: em corrida
    /// (<c>SqlException 2627/2601</c>) devolve <c>null</c> (a orquestração re-resolve),
    /// nunca "SELECT-then-INSERT" como única defesa (evita TOCTOU). Retorna o <c>id</c>
    /// recém-inserido em sucesso.
    /// </summary>
    Task<int?> TryInsertCiamUserAsync(Guid entraOid, string email, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-resolve por email (usado APÓS uma duplicata de INSERT, junto com
    /// <see cref="FindIdByEntraOidAsync"/>) — <c>SELECT id, entra_oid FROM dbo.users WHERE
    /// email = @email</c>. Retorna a identidade da linha (id + o oid já vinculado, se
    /// houver) para que a orquestração distinga "vinculado a NÓS por uma corrida" (resolve)
    /// de "email pertencente a OUTRA identidade CIAM" (conflito — flag de segurança (b) da
    /// Invariante 8, jamais devolve o id de uma linha de outra identidade).
    /// </summary>
    Task<UserIdentity?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}

/// <summary>
/// Projeção mínima de uma linha <c>users</c> para o re-resolve por email: o <c>id</c> e o
/// <c>entra_oid</c> atualmente vinculado (<c>null</c> se a linha nunca migrou).
/// </summary>
public sealed record UserIdentity(int Id, Guid? EntraOid);

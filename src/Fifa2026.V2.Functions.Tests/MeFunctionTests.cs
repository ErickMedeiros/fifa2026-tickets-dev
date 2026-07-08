using System.Text.Json;
using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// Story 3.5 (ADE-007 v1.2 Invariante 8) — MeFunction: contrato resolve-or-provision do
/// GET /api/v2/me (unificação base v1 ↔ CIAM). Testado no nível da Function com um
/// <see cref="IUserRepository"/> mockado (mesmo padrão de <c>PurchaseConsumerFunctionTests</c>):
/// a precedência determinística (oid → email-link → insert), a idempotência sob concorrência
/// (2627 → re-resolve, modelada como a primitiva devolvendo null) e o mapeamento HTTP são
/// exercitados sem um banco real.
/// </summary>
public sealed class MeFunctionTests
{
    private static readonly Guid Oid = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private const string Email = "joao@example.com";
    private const string Name = "João Silva";

    private static HttpRequest BuildRequest(string? oid = null, string? email = null, string? name = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        if (oid is not null) request.Headers["X-Entra-OID"] = oid;
        if (email is not null) request.Headers["X-Entra-Email"] = email;
        if (name is not null) request.Headers["X-Entra-Name"] = name;
        return request;
    }

    private static MeFunction Build(IUserRepository repo) =>
        new(repo, NullLogger<MeFunction>.Instance);

    /// <summary>Lê o campo `userId` do value anônimo de um OkObjectResult 200.</summary>
    private static int ReadUserId(OkObjectResult ok)
    {
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("userId").GetInt32();
    }

    // -------------------------------------------------------------------------
    // Fence de identidade na própria Function (defesa em profundidade — Inv 8 (a)):
    // sem X-Entra-OID válido, jamais resolve/provisiona.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NoOidHeader_Returns401_And_NeverTouchesRepository()
    {
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var sut = Build(repo.Object);

        var result = await sut.RunAsync(BuildRequest(oid: null, email: Email, name: Name), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidOidHeader_Returns401_And_NeverTouchesRepository()
    {
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        var sut = Build(repo.Object);

        var result = await sut.RunAsync(BuildRequest(oid: "not-a-guid", email: Email, name: Name), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        repo.VerifyNoOtherCalls();
    }

    // -------------------------------------------------------------------------
    // Passo 1 — resolve por oid (AC-8 regressão: já unificado → id existente, zero write).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResolveByOid_ReturnsExistingId_WithoutLinkOrInsert()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync(42);

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(42, ReadUserId(ok));
        repo.Verify(r => r.TryLinkByEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.TryInsertCiamUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Idempotent_SameOid_TwoCalls_ReturnSameId_NoInsert()
    {
        // AC-4/AC-9 — chamar 2x com o mesmo oid NÃO cria duplicata: ambas resolvem o mesmo id.
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync(42);

        var sut = Build(repo.Object);
        var first = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);
        var second = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);

        Assert.Equal(42, ReadUserId(Assert.IsType<OkObjectResult>(first)));
        Assert.Equal(42, ReadUserId(Assert.IsType<OkObjectResult>(second)));
        repo.Verify(r => r.TryInsertCiamUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Passo 2 — link por email (AC-2 passo 2): usuário v1 não-migrado chegando via CIAM.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LinkByEmail_WhenOidMissesButEmailExistsUnlinked_LinksNotInserts()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryLinkByEmailAsync(Oid, Email, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);

        Assert.Equal(7, ReadUserId(Assert.IsType<OkObjectResult>(result)));
        repo.Verify(r => r.TryInsertCiamUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Passo 3 — insert (nato-CIAM genuíno).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Insert_WhenOidAndEmailMiss_ProvisionsNewRow_ForwardingClaims()
    {
        Guid capturedOid = Guid.Empty;
        string? capturedEmail = null, capturedName = null;

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryLinkByEmailAsync(Oid, Email, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryInsertCiamUserAsync(Oid, Email, Name, It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, CancellationToken>((o, e, n, _) => { capturedOid = o; capturedEmail = e; capturedName = n; })
            .ReturnsAsync(100);

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);

        Assert.Equal(100, ReadUserId(Assert.IsType<OkObjectResult>(result)));
        // AC-6 — email/name propagados pelo gateway chegam ao INSERT (colunas NOT NULL).
        Assert.Equal(Oid, capturedOid);
        Assert.Equal(Email, capturedEmail);
        Assert.Equal(Name, capturedName);
    }

    // -------------------------------------------------------------------------
    // Idempotência sob concorrência (AC-4): a primitiva de INSERT devolve null numa corrida
    // (2627/2601 capturado no repo) → a orquestração RE-RESOLVE, nunca duplica.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InsertRace_DuplicateThenReResolveByOid_ReturnsWinnerId()
    {
        var repo = new Mock<IUserRepository>();
        // 1ª resolução por oid: miss. Após a duplicata: hit (o vencedor da corrida).
        repo.SetupSequence(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null)
            .ReturnsAsync(55);
        repo.Setup(r => r.TryLinkByEmailAsync(Oid, Email, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryInsertCiamUserAsync(Oid, Email, Name, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null); // corrida

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);

        Assert.Equal(55, ReadUserId(Assert.IsType<OkObjectResult>(result)));
        repo.Verify(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InsertRace_ReResolveByEmail_SameOid_Resolves()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryLinkByEmailAsync(Oid, Email, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryInsertCiamUserAsync(Oid, Email, Name, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        // re-resolve por email: linha vinculada concorrentemente a NÓS (mesmo oid) → resolve.
        repo.Setup(r => r.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(new UserIdentity(9, Oid));

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);

        Assert.Equal(9, ReadUserId(Assert.IsType<OkObjectResult>(result)));
    }

    [Fact]
    public async Task InsertRace_ReResolveByEmail_DifferentOid_ReturnsConflict()
    {
        // Flag de segurança (b) da Inv 8: o email pertence a OUTRA identidade CIAM → jamais
        // devolve o id dela nem sobrescreve. 409, não 200 com id alheio.
        var otherOid = Guid.Parse("bbbbbbbb-9999-8888-7777-666666666666");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryLinkByEmailAsync(Oid, Email, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryInsertCiamUserAsync(Oid, Email, Name, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(new UserIdentity(9, otherOid));

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: Name), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    // -------------------------------------------------------------------------
    // Claims insuficientes: sem email/name não há como criar a linha (colunas NOT NULL).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NoEmail_OidMisses_CannotProvision_Returns422()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: null, name: null), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, obj.StatusCode);
        // Sem email, o arm de LINK e o INSERT nunca são tentados.
        repo.Verify(r => r.TryLinkByEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.TryInsertCiamUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailButNoName_OidAndLinkMiss_CannotProvision_Returns422()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.FindIdByEntraOidAsync(Oid, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        repo.Setup(r => r.TryLinkByEmailAsync(Oid, Email, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var sut = Build(repo.Object);
        var result = await sut.RunAsync(BuildRequest(oid: Oid.ToString(), email: Email, name: null), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, obj.StatusCode);
        repo.Verify(r => r.TryInsertCiamUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

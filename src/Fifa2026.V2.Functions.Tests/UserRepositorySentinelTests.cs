using System.Text.RegularExpressions;
using Fifa2026.V2.Functions.Data;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// Story 3.5 (ADE-007 v1.2 Invariante 8.1) — prova de que a SENTINELA de <c>password</c>
/// gravada num usuário nato-CIAM é <b>fail-closed por construção</b>: NÃO é um hash bcrypt
/// válido, logo <c>bcryptjs.compare(qualquer_entrada, SENTINELA)</c> resolve <c>false</c>
/// SEMPRE (<c>fifa2026-api/src/routes/auth.js:95</c>) — o login v1 nunca autentica um
/// nato-CIAM. Este é o proxy determinístico (roda em <c>dotnet test</c>) da verificação
/// empírica com o próprio <c>bcryptjs</c> feita durante a implementação (AC-5).
/// </summary>
public sealed class UserRepositorySentinelTests
{
    // Formato de um hash bcrypt: $2[aby]$ + custo(2 dígitos) + $ + 22 chars de salt + 31 de
    // hash = 60 chars no total. bcryptjs SÓ retorna true para uma string neste formato; para
    // qualquer coisa fora dele, compare() devolve false (não lança) — o que fecha a porta v1.
    private static readonly Regex BcryptHash =
        new(@"^\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}$", RegexOptions.Compiled);

    [Fact]
    public void Sentinel_Is_Not_A_Valid_Bcrypt_Hash()
    {
        Assert.DoesNotMatch(BcryptHash, UserRepository.CiamManagedPasswordSentinel);
    }

    [Fact]
    public void Sentinel_Does_Not_Start_With_Bcrypt_Prefix()
    {
        // Um valor que não começa com "$2" nunca é sequer parseável como bcrypt.
        Assert.DoesNotContain(UserRepository.CiamManagedPasswordSentinel, new[] { "$2a$", "$2b$", "$2y$" });
        Assert.False(UserRepository.CiamManagedPasswordSentinel.StartsWith("$2", StringComparison.Ordinal));
    }

    [Fact]
    public void Sentinel_Is_A_Documented_NonEmpty_Marker()
    {
        // Marca legível e estável (não um valor aleatório opaco), para diagnóstico no banco.
        Assert.False(string.IsNullOrWhiteSpace(UserRepository.CiamManagedPasswordSentinel));
        Assert.Equal("CIAM_MANAGED__NO_LOCAL_PASSWORD", UserRepository.CiamManagedPasswordSentinel);
    }
}

using System.Security.Claims;

namespace Questions.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Papel do usuário autenticado. A decisão sobre o que esse papel pode fazer
    /// é tomada pelo núcleo F# (módulo Authorization), não aqui.
    /// </summary>
    public static string Role(this ClaimsPrincipal user) =>
        user.FindFirstValue(AuthClaims.Role) ?? string.Empty;
}

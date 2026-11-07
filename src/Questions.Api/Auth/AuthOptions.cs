using System.ComponentModel.DataAnnotations;

namespace Questions.Api.Auth;

/// <summary>Configuração de autenticação (seção "Auth").</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string Issuer { get; init; } = "fsharp-questions";

    [Required]
    public string Audience { get; init; } = "fsharp-questions-api";

    /// <summary>Chave simétrica HMAC-SHA256 (mínimo 32 caracteres).</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 24 * 60)]
    public int TokenLifetimeMinutes { get; init; } = 60;

    /// <summary>Usuários de demonstração. Em produção, substitua por um provedor de identidade.</summary>
    public IReadOnlyList<UserCredential> Users { get; init; } = [];
}

public sealed class UserCredential
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public static class AuthClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
}

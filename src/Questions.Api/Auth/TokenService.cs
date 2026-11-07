using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Questions.Api.Auth;

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt, string Role);

/// <summary>Valida credenciais e emite tokens JWT.</summary>
public sealed class TokenService(IOptions<AuthOptions> options, TimeProvider clock)
{
    private readonly AuthOptions _options = options.Value;

    public static SymmetricSecurityKey CreateSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));

    public IssuedToken? TryIssue(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || password is null)
        {
            return null;
        }

        var user = _options.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        // Compara mesmo quando o usuário não existe, para não vazar essa informação pelo tempo de resposta.
        var matches = FixedTimeEquals(user?.Password ?? string.Empty, password);
        if (user is null || !matches)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.TokenLifetimeMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(AuthClaims.Subject, user.Username),
                new Claim(AuthClaims.Role, user.Role),
            ]),
            SigningCredentials = new SigningCredentials(
                CreateSigningKey(_options.SigningKey),
                SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt, user.Role);
    }

    private static bool FixedTimeEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(actual)));
}

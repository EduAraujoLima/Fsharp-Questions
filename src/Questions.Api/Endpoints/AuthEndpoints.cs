using Questions.Api.Auth;
using Questions.Api.Contracts;

namespace Questions.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/token", IssueToken)
            .WithTags("Autenticação")
            .WithName("IssueToken")
            .WithSummary("Autentica um usuário e emite um token JWT.")
            .AllowAnonymous()
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static IResult IssueToken(LoginRequest request, TokenService tokens) =>
        tokens.TryIssue(request.Username, request.Password) is { } token
            ? Results.Ok(new TokenResponse(token.AccessToken, "Bearer", token.ExpiresAt, token.Role))
            : Results.Problem(
                detail: "Usuário ou senha inválidos.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Não autenticado");
}

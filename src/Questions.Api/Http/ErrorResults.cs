using Questions.Core.Contracts;

namespace Questions.Api.Http;

/// <summary>Traduz os erros (dados) do núcleo F# para respostas HTTP (RFC 9457 – Problem Details).</summary>
public static class ErrorResults
{
    public static IResult ToHttpResult(this ErrorView error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Kind.IsInvalidInput)
        {
            var fieldErrors = error.Errors
                .GroupBy(e => e.Field)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

            extensions["details"] = error.Errors
                .Select(e => new { field = e.Field, code = e.Code, message = e.Message })
                .ToList();

            return Results.ValidationProblem(
                fieldErrors,
                detail: error.Message,
                title: "Dados inválidos",
                extensions: extensions);
        }

        var (status, title) = error.Kind.Tag switch
        {
            ErrorKind.Tags.NotFound => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ErrorKind.Tags.Forbidden => (StatusCodes.Status403Forbidden, "Acesso negado"),
            ErrorKind.Tags.BusinessRuleViolation => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno"),
        };

        return Results.Problem(detail: error.Message, statusCode: status, title: title, extensions: extensions);
    }

    public static IResult Conflict(Guid questionId) =>
        Results.Problem(
            detail: $"A questão {questionId} foi alterada por outra requisição. Recarregue-a e tente novamente.",
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflito de versão",
            extensions: new Dictionary<string, object?> { ["code"] = "version_conflict" });

    public static IResult NotFound(Guid questionId) =>
        Results.Problem(
            detail: $"Questão {questionId} não encontrada.",
            statusCode: StatusCodes.Status404NotFound,
            title: "Recurso não encontrado",
            extensions: new Dictionary<string, object?> { ["code"] = "question_not_found" });
}

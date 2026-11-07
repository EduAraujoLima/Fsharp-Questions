using System.Security.Claims;
using Questions.Api.Auth;
using Questions.Api.Contracts;
using Questions.Api.Http;
using Questions.Api.Services;
using Questions.Core;
using Questions.Core.Contracts;
using Questions.Infrastructure.Interop;
using Questions.Infrastructure.Persistence;

namespace Questions.Api.Endpoints;

/// <summary>
/// Shell imperativo das questões: cada handler (1) coleta os efeitos necessários (papel do
/// usuário, id, relógio, dados do banco), (2) delega a decisão ao núcleo F# e (3) executa
/// o efeito decidido (persistir/responder).
/// </summary>
public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/question-types", ListQuestionTypes)
            .WithTags("Questões")
            .WithName("ListQuestionTypes")
            .WithSummary("Lista os tipos de questão suportados e suas regras.")
            .AllowAnonymous()
            .Produces<IReadOnlyList<QuestionTypeResponse>>();

        var group = app.MapGroup("/api/questions")
            .WithTags("Questões")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", ListQuestions)
            .WithName("ListQuestions")
            .WithSummary("Lista o banco de questões com filtros e paginação (Admin).")
            .Produces<PagedResponse<QuestionResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetQuestion)
            .WithName("GetQuestion")
            .WithSummary("Obtém uma questão com o gabarito (Admin).")
            .Produces<QuestionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateQuestion)
            .WithName("CreateQuestion")
            .WithSummary("Cadastra uma questão (Admin).")
            .Produces<QuestionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateQuestion)
            .WithName("UpdateQuestion")
            .WithSummary("Altera uma questão, gerando uma nova versão (Admin).")
            .Produces<QuestionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteQuestion)
            .WithName("DeleteQuestion")
            .WithSummary("Exclui uma questão (Admin).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult ListQuestionTypes() =>
        Results.Ok(Workflows.QuestionTypes.Select(t => t.ToResponse()).ToList());

    private static async Task<IResult> ListQuestions(
        ClaimsPrincipal user,
        IQuestionRepository repository,
        CancellationToken cancellationToken,
        string? type = null,
        string? difficulty = null,
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        var plan = Workflows.ListQuestions(user.Role(), new QuestionQuery(type, difficulty, search, page, pageSize));
        if (plan.IsError)
        {
            return plan.ErrorValue.ToHttpResult();
        }

        var bank = await repository.ListAsync(cancellationToken);
        return plan.ResultValue.Invoke(bank)
            .Match(result => Results.Ok(result.ToResponse()), ErrorResults.ToHttpResult);
    }

    private static async Task<IResult> GetQuestion(
        Guid id,
        ClaimsPrincipal user,
        IQuestionRepository repository,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(id, cancellationToken);
        return Workflows.GetQuestion(user.Role(), id, existing.ToFSharpOption())
            .Match(question => Results.Ok(question.ToResponse()), ErrorResults.ToHttpResult);
    }

    private static async Task<IResult> CreateQuestion(
        QuestionRequest request,
        ClaimsPrincipal user,
        IQuestionRepository repository,
        IIdGenerator ids,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        await Workflows.CreateQuestion(user.Role(), ids.NewId(), clock.GetUtcNow(), request.ToInput())
            .MatchAsync(
                async question =>
                {
                    await repository.AddAsync(question, cancellationToken);
                    return Results.Created($"/api/questions/{question.Id}", question.ToResponse());
                },
                ErrorResults.ToHttpResult);

    private static async Task<IResult> UpdateQuestion(
        Guid id,
        QuestionRequest request,
        ClaimsPrincipal user,
        IQuestionRepository repository,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(id, cancellationToken);

        return await Workflows.UpdateQuestion(user.Role(), clock.GetUtcNow(), id, request.ToInput(), existing.ToFSharpOption())
            .MatchAsync(
                async question => await repository.UpdateAsync(question, cancellationToken) switch
                {
                    SaveOutcome.Saved => Results.Ok(question.ToResponse()),
                    SaveOutcome.NotFound => ErrorResults.NotFound(id),
                    _ => ErrorResults.Conflict(id),
                },
                ErrorResults.ToHttpResult);
    }

    private static async Task<IResult> DeleteQuestion(
        Guid id,
        ClaimsPrincipal user,
        IQuestionRepository repository,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(id, cancellationToken);

        return await Workflows.DeleteQuestion(user.Role(), id, existing.ToFSharpOption())
            .MatchAsync(
                async questionId => await repository.DeleteAsync(questionId, cancellationToken) switch
                {
                    SaveOutcome.Saved => Results.NoContent(),
                    _ => ErrorResults.NotFound(questionId),
                },
                ErrorResults.ToHttpResult);
    }
}

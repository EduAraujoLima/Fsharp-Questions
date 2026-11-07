using System.Security.Claims;
using Questions.Api.Auth;
using Questions.Api.Contracts;
using Questions.Api.Http;
using Questions.Api.Services;
using Questions.Core;
using Questions.Infrastructure.Interop;
using Questions.Infrastructure.Persistence;

namespace Questions.Api.Endpoints;

public static class ExamEndpoints
{
    public static IEndpointRouteBuilder MapExamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/exams")
            .WithTags("Provas")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", GenerateExam)
            .WithName("GenerateExam")
            .WithSummary("Gera uma prova sorteando questões do banco.")
            .WithDescription(
                "Cada seção pede N questões de um tipo e/ou dificuldade. As questões não se repetem, " +
                "a ordem das questões é aleatória e as alternativas podem ser embaralhadas. " +
                "Informe 'seed' para reproduzir exatamente a mesma prova.")
            .Produces<ExamResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/grade", GradeExam)
            .WithName("GradeExam")
            .WithSummary("Corrige automaticamente as respostas de uma prova.")
            .Produces<GradeReportResponse>()
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> GenerateExam(
        GenerateExamRequest request,
        ClaimsPrincipal user,
        IQuestionRepository repository,
        ISeedProvider seeds,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var seed = request.Seed ?? seeds.NextSeed();
        var plan = Workflows.GenerateExam(user.Role(), seed, clock.GetUtcNow(), request.ToInput());
        if (plan.IsError)
        {
            return plan.ErrorValue.ToHttpResult();
        }

        var bank = await repository.ListAsync(cancellationToken);
        return plan.ResultValue.Invoke(bank)
            .Match(exam => Results.Ok(exam.ToResponse()), ErrorResults.ToHttpResult);
    }

    private static async Task<IResult> GradeExam(
        GradeExamRequest request,
        ClaimsPrincipal user,
        IQuestionRepository repository,
        CancellationToken cancellationToken)
    {
        var plan = Workflows.GradeExam(user.Role(), request.ToInput());
        if (plan.IsError)
        {
            return plan.ErrorValue.ToHttpResult();
        }

        var referenced = (request.Answers ?? []).Select(a => a.QuestionId);
        var bank = await repository.ListByIdsAsync(referenced, cancellationToken);
        return plan.ResultValue.Invoke(bank)
            .Match(report => Results.Ok(report.ToResponse()), ErrorResults.ToHttpResult);
    }
}

using System.Net;
using System.Net.Http.Json;
using Questions.Api.Contracts;

namespace Questions.Api.Tests;

public sealed class ExamEndpointsTests
{
    private static async Task<HttpClient> SeedBankAsync(ApiFactory api)
    {
        var admin = await api.CreateAdminClientAsync();
        for (var i = 1; i <= 6; i++)
        {
            await admin.CreateQuestionAsync(Requests.SingleChoice($"Única {i}"));
        }

        for (var i = 1; i <= 3; i++)
        {
            await admin.CreateQuestionAsync(Requests.TrueFalse($"VF {i}", answer: i % 2 == 0));
        }

        await admin.CreateQuestionAsync(Requests.Essay("Dissertativa"));
        return admin;
    }

    [Fact]
    public async Task Aluno_gera_prova_aleatoria_sem_gabarito()
    {
        await using var api = new ApiFactory();
        await SeedBankAsync(api);
        var student = await api.CreateStudentClientAsync();

        var response = await student.PostAsJsonAsync("/api/exams",
            new GenerateExamRequest("Prova 1", [new("SingleChoice", null, 3), new("TrueFalse", null, 2), new(null, null, 1)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exam = (await response.Content.ReadFromJsonAsync<ExamResponse>())!;
        Assert.Equal(6, exam.TotalQuestions);
        Assert.Equal(6, exam.Questions.Select(q => q.QuestionId).Distinct().Count());
        Assert.True(exam.Questions.Count(q => q.Type == "SingleChoice") >= 3);
        Assert.True(exam.Questions.Count(q => q.Type == "TrueFalse") >= 2);
        Assert.Null(exam.AnswerKey);
        Assert.All(exam.Questions, q => Assert.Equal(
            Enumerable.Range(0, q.Options.Count).Select(i => ((char)('A' + i)).ToString()),
            q.Options.Select(o => o.Label)));
    }

    [Fact]
    public async Task Mesma_semente_reproduz_a_mesma_prova()
    {
        await using var api = new ApiFactory();
        await SeedBankAsync(api);
        var student = await api.CreateStudentClientAsync();
        var request = new GenerateExamRequest("Prova", [new(null, null, 8)], Seed: 12345);

        var first = await (await student.PostAsJsonAsync("/api/exams", request)).Content.ReadFromJsonAsync<ExamResponse>();
        var second = await (await student.PostAsJsonAsync("/api/exams", request)).Content.ReadFromJsonAsync<ExamResponse>();

        Assert.Equal(12345, first!.Seed);
        Assert.Equal(
            first.Questions.Select(q => (q.QuestionId, string.Join(',', q.Options.Select(o => o.Id)))),
            second!.Questions.Select(q => (q.QuestionId, string.Join(',', q.Options.Select(o => o.Id)))));
    }

    [Fact]
    public async Task Gabarito_apenas_para_admin()
    {
        await using var api = new ApiFactory();
        var admin = await SeedBankAsync(api);
        var student = await api.CreateStudentClientAsync();
        var request = new GenerateExamRequest("Prova", [new("SingleChoice", null, 2)], IncludeAnswerKey: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync("/api/exams", request)).StatusCode);

        var exam = await (await admin.PostAsJsonAsync("/api/exams", request)).Content.ReadFromJsonAsync<ExamResponse>();
        Assert.Equal(2, exam!.AnswerKey!.Count);
        Assert.All(exam.AnswerKey, key => Assert.Equal([1], key.CorrectOptionIds));
    }

    [Fact]
    public async Task Banco_insuficiente_retorna_422()
    {
        await using var api = new ApiFactory();
        await SeedBankAsync(api);
        var student = await api.CreateStudentClientAsync();

        var response = await student.PostAsJsonAsync("/api/exams",
            new GenerateExamRequest("Prova", [new("Essay", null, 2)]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Pedido_de_prova_invalido_retorna_400()
    {
        await using var api = new ApiFactory();
        var student = await api.CreateStudentClientAsync();

        var response = await student.PostAsJsonAsync("/api/exams",
            new GenerateExamRequest("", [new("Quiz", null, 0)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Correcao_automatica_da_prova()
    {
        await using var api = new ApiFactory();
        var admin = await api.CreateAdminClientAsync();
        var single = await admin.CreateQuestionAsync(Requests.SingleChoice("Única"));
        var trueFalse = await admin.CreateQuestionAsync(Requests.TrueFalse("VF", answer: false));
        var essay = await admin.CreateQuestionAsync(Requests.Essay("Dissertativa"));
        var student = await api.CreateStudentClientAsync();

        var response = await student.PostAsJsonAsync("/api/exams/grade", new GradeExamRequest(
        [
            new(single.Id, [1], null),
            new(trueFalse.Id, [1], null),
            new(essay.Id, null, "Minha resposta"),
        ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<GradeReportResponse>())!;
        Assert.Equal(["Correct", "Incorrect", "PendingReview"], report.Results.Select(r => r.Outcome));
        Assert.Equal(50m, report.ScorePercent);

        var invalid = await student.PostAsJsonAsync("/api/exams/grade",
            new GradeExamRequest([new(Guid.NewGuid(), [1], null)]));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}

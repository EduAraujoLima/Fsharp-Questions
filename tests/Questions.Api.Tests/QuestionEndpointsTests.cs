using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Questions.Api.Contracts;

namespace Questions.Api.Tests;

public sealed class QuestionEndpointsTests
{
    [Fact]
    public async Task Admin_cadastra_consulta_altera_e_exclui_questao()
    {
        await using var api = new ApiFactory();
        var admin = await api.CreateAdminClientAsync();

        // Cadastro
        var createResponse = await admin.PostAsJsonAsync("/api/questions", Requests.SingleChoice("Quanto é 2 + 2?"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<QuestionResponse>())!;
        Assert.Equal($"/api/questions/{created.Id}", createResponse.Headers.Location!.OriginalString);
        Assert.Equal(1, created.Version);
        Assert.Equal([1, 2, 3], created.Options.Select(o => o.Id));

        // Consulta
        var fetched = await admin.GetFromJsonAsync<QuestionResponse>($"/api/questions/{created.Id}");
        Assert.Equivalent(created, fetched, strict: true);

        // Alteração (inclusive de tipo)
        var updateResponse = await admin.PutAsJsonAsync($"/api/questions/{created.Id}", Requests.Essay("Explique 2 + 2."));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<QuestionResponse>())!;
        Assert.Equal(2, updated.Version);
        Assert.Equal("Essay", updated.Type);
        Assert.Empty(updated.Options);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);

        var afterUpdate = await admin.GetFromJsonAsync<QuestionResponse>($"/api/questions/{created.Id}");
        Assert.Equivalent(updated, afterUpdate, strict: true);

        // Exclusão
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/questions/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/questions/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/questions/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Cadastro_invalido_retorna_400_com_todos_os_erros()
    {
        await using var api = new ApiFactory();
        var admin = await api.CreateAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/questions",
            new QuestionRequest("TrueFalse", "", "Easy", [new("V", true), new("F", true), new("Talvez", false)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var codes = problem.RootElement.GetProperty("details").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .ToList();
        Assert.Equal(["statement_required", "too_many_options", "exactly_one_correct_option_required"], codes);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("statement", out _));
    }

    [Fact]
    public async Task Admin_lista_questoes_com_filtros_e_paginacao()
    {
        await using var api = new ApiFactory();
        var admin = await api.CreateAdminClientAsync();
        await admin.CreateQuestionAsync(Requests.SingleChoice("Única A"));
        await admin.CreateQuestionAsync(Requests.SingleChoice("Única B", "Hard"));
        await admin.CreateQuestionAsync(Requests.TrueFalse("VF A"));
        await admin.CreateQuestionAsync(Requests.Essay("Dissertativa A"));

        var all = await admin.GetFromJsonAsync<PagedResponse<QuestionResponse>>("/api/questions");
        Assert.Equal(4, all!.TotalItems);

        var singles = await admin.GetFromJsonAsync<PagedResponse<QuestionResponse>>(
            "/api/questions?type=SingleChoice&pageSize=1&page=2");
        Assert.Equal(2, singles!.TotalItems);
        Assert.Equal(2, singles.TotalPages);
        Assert.Single(singles.Items);

        var hard = await admin.GetFromJsonAsync<PagedResponse<QuestionResponse>>("/api/questions?difficulty=hard");
        Assert.Equal(["Dissertativa A", "Única B"], hard!.Items.Select(q => q.Statement).Order());

        var search = await admin.GetFromJsonAsync<PagedResponse<QuestionResponse>>("/api/questions?search=vf");
        Assert.Equal("VF A", Assert.Single(search!.Items).Statement);

        var invalid = await admin.GetAsync("/api/questions?pageSize=1000");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Aluno_nao_gerencia_nem_lista_questoes()
    {
        await using var api = new ApiFactory();
        var admin = await api.CreateAdminClientAsync();
        var student = await api.CreateStudentClientAsync();
        var question = await admin.CreateQuestionAsync(Requests.SingleChoice("Q"));

        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/questions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/questions/{question.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PostAsJsonAsync("/api/questions", Requests.SingleChoice("Q2"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PutAsJsonAsync($"/api/questions/{question.Id}", Requests.Essay("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.DeleteAsync($"/api/questions/{question.Id}")).StatusCode);
    }

    [Fact]
    public async Task Alterar_questao_inexistente_retorna_404()
    {
        await using var api = new ApiFactory();
        var admin = await api.CreateAdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/questions/{Guid.NewGuid()}", Requests.Essay("x"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

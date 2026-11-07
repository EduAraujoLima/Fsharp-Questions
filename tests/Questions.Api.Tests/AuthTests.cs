using System.Net;
using System.Net.Http.Json;
using Questions.Api.Contracts;

namespace Questions.Api.Tests;

public sealed class AuthTests
{
    [Fact]
    public async Task Login_com_credenciais_validas_emite_token_com_papel()
    {
        await using var api = new ApiFactory();
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/token", new LoginRequest("admin", "admin123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.Equal("Admin", token!.Role);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
    }

    [Theory]
    [InlineData("admin", "errada")]
    [InlineData("ninguem", "admin123")]
    [InlineData("", "")]
    public async Task Login_invalido_retorna_401(string username, string password)
    {
        await using var api = new ApiFactory();
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/token", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_protegidos_exigem_token()
    {
        await using var api = new ApiFactory();
        var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/questions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/exams", new GenerateExamRequest("P", [new(null, null, 1)]))).StatusCode);
    }

    [Fact]
    public async Task Tipos_de_questao_sao_publicos()
    {
        await using var api = new ApiFactory();
        var types = await api.CreateClient().GetFromJsonAsync<List<QuestionTypeResponse>>("/api/question-types");
        Assert.Equal(["SingleChoice", "MultipleChoice", "TrueFalse", "Essay"], types!.Select(t => t.Code));
    }
}

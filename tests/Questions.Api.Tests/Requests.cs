using System.Net.Http.Json;
using Questions.Api.Contracts;

namespace Questions.Api.Tests;

internal static class Requests
{
    public static QuestionRequest SingleChoice(string statement, string difficulty = "Easy") =>
        new("SingleChoice", statement, difficulty,
            [new("Certa", true), new("Errada 1", false), new("Errada 2", false)]);

    public static QuestionRequest TrueFalse(string statement, bool answer = true) =>
        new("TrueFalse", statement, "Medium", [new("Verdadeiro", answer), new("Falso", !answer)]);

    public static QuestionRequest Essay(string statement) => new("Essay", statement, "Hard", []);

    public static async Task<QuestionResponse> CreateQuestionAsync(this HttpClient client, QuestionRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/questions", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QuestionResponse>())!;
    }
}

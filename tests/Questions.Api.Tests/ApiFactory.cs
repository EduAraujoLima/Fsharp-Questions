using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Questions.Api.Contracts;

namespace Questions.Api.Tests;

/// <summary>Sobe a API em memória com um banco SQLite temporário e exclusivo.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"questions-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Questions", $"Data Source={_databasePath}");
        builder.UseSetting("Seed:DemoData", "false");
    }

    public async Task<HttpClient> CreateClientAsAsync(string username, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() => CreateClientAsAsync("admin", "admin123");

    public Task<HttpClient> CreateStudentClientAsync() => CreateClientAsAsync("aluno", "aluno123");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}

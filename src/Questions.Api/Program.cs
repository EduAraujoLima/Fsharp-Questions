using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Questions.Api.Auth;
using Questions.Api.Endpoints;
using Questions.Api.Seeding;
using Questions.Api.Services;
using Questions.Infrastructure;
using Questions.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- Serviços do "mundo"
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IIdGenerator, GuidV7Generator>();
builder.Services.AddSingleton<ISeedProvider, CryptoSeedProvider>();
builder.Services.AddScoped<DemoDataSeeder>();

builder.Services.AddQuestionsInfrastructure(
    builder.Configuration.GetConnectionString("Questions") ?? "Data Source=questions.db");

// ---------------------------------------------------------------- Autenticação (JWT)
builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>>((jwt, auth) =>
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = auth.Value.Issuer,
            ValidAudience = auth.Value.Audience,
            IssuerSigningKey = TokenService.CreateSigningKey(auth.Value.SigningKey),
            NameClaimType = AuthClaims.Subject,
            RoleClaimType = AuthClaims.Role,
        };
    });
// A autorização do ASP.NET só garante que o usuário está autenticado;
// as permissões de negócio são decididas pelo núcleo F#.
builder.Services.AddAuthorization();

// ---------------------------------------------------------------- HTTP
builder.Services.AddProblemDetails();
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.AllowTrailingCommas = true);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "F# Questions API",
            Version = "v1",
            Description = "Banco de questões e geração de provas. Regras de negócio em F# (puro); I/O em C#.",
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Obtenha o token em POST /api/auth/token.",
        };
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// ---------------------------------------------------------------- Banco de dados
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuestionsDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (app.Configuration.GetValue("Seed:DemoData", false))
    {
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }
}

// ---------------------------------------------------------------- Pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("F# Questions API"));
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

app.MapAuthEndpoints();
app.MapQuestionEndpoints();
app.MapExamEndpoints();

await app.RunAsync();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;

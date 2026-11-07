using Questions.Api.Contracts;
using Questions.Api.Services;
using Questions.Core;
using Questions.Core.Contracts;
using Questions.Infrastructure.Interop;
using Questions.Infrastructure.Persistence;

namespace Questions.Api.Seeding;

/// <summary>
/// Popula um banco vazio com questões de exemplo. As questões passam pelo mesmo
/// caso de uso F# do endpoint de cadastro, então também são validadas pelas regras de negócio.
/// </summary>
public sealed class DemoDataSeeder(
    IQuestionRepository repository,
    IIdGenerator ids,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    private const string SeederRole = "Admin";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await repository.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var request in Samples)
        {
            var created = Workflows.CreateQuestion(SeederRole, ids.NewId(), clock.GetUtcNow(), request.ToInput());
            await created.MatchAsync(
                async question =>
                {
                    await repository.AddAsync(question, cancellationToken);
                    return true;
                },
                error =>
                {
                    logger.LogWarning("Questão de exemplo inválida ignorada: {Message}", error.Message);
                    return false;
                });
        }

        logger.LogInformation("Banco de questões populado com {Count} questões de exemplo.", Samples.Count);
    }

    private static OptionRequest Right(string text) => new(text, true);

    private static OptionRequest Wrong(string text) => new(text, false);

    private static readonly IReadOnlyList<QuestionRequest> Samples =
    [
        new("SingleChoice", "Qual palavra-chave declara um valor imutável em F#?", "Easy",
            [Right("let"), Wrong("var"), Wrong("mutable"), Wrong("val")]),
        new("SingleChoice", "Qual operador aplica um valor a uma função em F# (pipe)?", "Easy",
            [Wrong(">>"), Right("|>"), Wrong("<-"), Wrong("::")]),
        new("SingleChoice", "O operador >> em F# representa:", "Medium",
            [Right("Composição de funções"), Wrong("Deslocamento de bits"), Wrong("Pipe reverso"), Wrong("Concatenação de listas")]),
        new("SingleChoice", "Qual tipo do F# representa um cálculo que pode falhar com um erro tipado?", "Medium",
            [Wrong("Option<'T>"), Right("Result<'T, 'TError>"), Wrong("Lazy<'T>"), Wrong("Async<'T>")]),
        new("MultipleChoice", "Quais das opções são características de uma função pura?", "Medium",
            [Right("Sempre retorna o mesmo resultado para as mesmas entradas"), Right("Não produz efeitos colaterais"),
             Wrong("Pode alterar variáveis globais"), Wrong("Depende da hora atual do sistema")]),
        new("MultipleChoice", "Quais construções do F# ajudam a tornar estados inválidos irrepresentáveis?", "Hard",
            [Right("Uniões discriminadas"), Right("Construtores privados com smart constructors"),
             Wrong("Variáveis mutáveis"), Wrong("Conversões implícitas")]),
        new("MultipleChoice", "Quais são estruturas de dados imutáveis da FSharp.Core?", "Medium",
            [Right("list"), Right("Map"), Right("Set"), Wrong("array")]),
        new("TrueFalse", "Em F#, registros (records) são imutáveis por padrão.", "Easy",
            [Right("Verdadeiro"), Wrong("Falso")]),
        new("TrueFalse", "Em F#, a ordem dos arquivos no projeto é irrelevante para a compilação.", "Medium",
            [Wrong("Verdadeiro"), Right("Falso")]),
        new("TrueFalse", "Uma validação aplicativa acumula todos os erros em vez de parar no primeiro.", "Hard",
            [Right("Verdadeiro"), Wrong("Falso")]),
        new("Essay", "Explique a diferença entre composição de funções (>>) e o operador pipe (|>).", "Medium", []),
        new("Essay", "Descreva o padrão \"functional core, imperative shell\" e suas vantagens.", "Hard", []),
    ];
}

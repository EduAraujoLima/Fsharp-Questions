namespace Questions.Api.Contracts;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record OptionRequest(string? Text, bool IsCorrect);

/// <summary>Cadastro/alteração de questão.</summary>
/// <param name="Type">SingleChoice, MultipleChoice, TrueFalse ou Essay.</param>
/// <param name="Statement">Enunciado.</param>
/// <param name="Difficulty">Easy, Medium (padrão) ou Hard.</param>
/// <param name="Options">Alternativas (vazio para Essay).</param>
public sealed record QuestionRequest(
    string? Type,
    string? Statement,
    string? Difficulty,
    IReadOnlyList<OptionRequest>? Options);

/// <summary>Uma seção da prova: <c>Count</c> questões do tipo/dificuldade informados (ambos opcionais).</summary>
public sealed record ExamSectionRequest(string? Type, string? Difficulty, int Count);

/// <summary>Pedido de geração de prova.</summary>
/// <param name="Title">Título da prova.</param>
/// <param name="Sections">Composição da prova.</param>
/// <param name="ShuffleOptions">Embaralhar as alternativas de cada questão (padrão: true).</param>
/// <param name="IncludeAnswerKey">Incluir gabarito (somente Admin).</param>
/// <param name="Seed">Semente opcional para reproduzir exatamente a mesma prova.</param>
public sealed record GenerateExamRequest(
    string? Title,
    IReadOnlyList<ExamSectionRequest>? Sections,
    bool ShuffleOptions = true,
    bool IncludeAnswerKey = false,
    long? Seed = null);

public sealed record AnswerRequest(Guid QuestionId, IReadOnlyList<int>? SelectedOptionIds, string? Text);

public sealed record GradeExamRequest(IReadOnlyList<AnswerRequest>? Answers);

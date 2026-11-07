namespace Questions.Api.Contracts;

public sealed record TokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, string Role);

public sealed record OptionResponse(int Id, string Text, bool IsCorrect);

public sealed record QuestionResponse(
    Guid Id,
    string Type,
    string Statement,
    string Difficulty,
    IReadOnlyList<OptionResponse> Options,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record QuestionTypeResponse(
    string Code,
    string Description,
    bool AcceptsOptions,
    int MinOptions,
    int MaxOptions,
    string CorrectOptions);

public sealed record ExamOptionResponse(int Id, string Label, string Text);

public sealed record ExamQuestionResponse(
    int Number,
    Guid QuestionId,
    string Type,
    string Difficulty,
    string Statement,
    IReadOnlyList<ExamOptionResponse> Options);

public sealed record AnswerKeyEntryResponse(
    int Number,
    Guid QuestionId,
    IReadOnlyList<int> CorrectOptionIds,
    IReadOnlyList<string> CorrectLabels);

public sealed record ExamResponse(
    string Title,
    long Seed,
    DateTimeOffset GeneratedAt,
    int TotalQuestions,
    IReadOnlyList<ExamQuestionResponse> Questions,
    IReadOnlyList<AnswerKeyEntryResponse>? AnswerKey);

public sealed record QuestionResultResponse(
    Guid QuestionId,
    string Type,
    string Outcome,
    IReadOnlyList<int> SelectedOptionIds,
    IReadOnlyList<int> CorrectOptionIds,
    decimal Score);

public sealed record GradeReportResponse(
    int TotalQuestions,
    int Correct,
    int Incorrect,
    int Unanswered,
    int PendingReview,
    decimal ScorePercent,
    IReadOnlyList<QuestionResultResponse> Results);

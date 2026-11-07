using Questions.Core.Contracts;
using Questions.Infrastructure.Interop;

namespace Questions.Api.Contracts;

/// <summary>Tradução entre o contrato HTTP (C#) e os contratos do núcleo funcional (F#).</summary>
public static class ContractMapping
{
    // ------------------------------ HTTP -> F# ------------------------------

    public static QuestionInput ToInput(this QuestionRequest request) =>
        new(
            request.Type,
            request.Statement,
            request.Difficulty,
            (request.Options ?? []).Select(o => new OptionInput(o.Text, o.IsCorrect)).ToFSharpList());

    public static ExamInput ToInput(this GenerateExamRequest request) =>
        new(
            request.Title,
            (request.Sections ?? []).Select(s => new ExamSectionInput(s.Type, s.Difficulty, s.Count)).ToFSharpList(),
            request.ShuffleOptions,
            request.IncludeAnswerKey);

    public static SubmissionInput ToInput(this GradeExamRequest request) =>
        new(
            (request.Answers ?? [])
                .Select(a => new AnswerInput(a.QuestionId, (a.SelectedOptionIds ?? []).ToFSharpList(), a.Text))
                .ToFSharpList());

    // ------------------------------ F# -> HTTP ------------------------------

    public static QuestionResponse ToResponse(this QuestionSnapshot question) =>
        new(
            question.Id,
            question.Type,
            question.Statement,
            question.Difficulty,
            question.Options.Select(o => new OptionResponse(o.Id, o.Text, o.IsCorrect)).ToList(),
            question.Version,
            question.CreatedAt,
            question.UpdatedAt);

    public static PagedResponse<QuestionResponse> ToResponse(this QuestionPage page) =>
        new(
            page.Items.Select(ToResponse).ToList(),
            page.Page,
            page.PageSize,
            page.TotalItems,
            page.TotalPages);

    public static QuestionTypeResponse ToResponse(this QuestionTypeInfo info) =>
        new(info.Code, info.Description, info.AcceptsOptions, info.MinOptions, info.MaxOptions, info.CorrectOptions);

    public static ExamResponse ToResponse(this ExamView exam) =>
        new(
            exam.Title,
            exam.Seed,
            exam.GeneratedAt,
            exam.TotalQuestions,
            exam.Questions
                .Select(q => new ExamQuestionResponse(
                    q.Number,
                    q.QuestionId,
                    q.Type,
                    q.Difficulty,
                    q.Statement,
                    q.Options.Select(o => new ExamOptionResponse(o.Id, o.Label, o.Text)).ToList()))
                .ToList(),
            exam.IncludesAnswerKey
                ? exam.AnswerKey
                    .Select(k => new AnswerKeyEntryResponse(
                        k.Number,
                        k.QuestionId,
                        k.CorrectOptionIds.ToList(),
                        k.CorrectLabels.ToList()))
                    .ToList()
                : null);

    public static GradeReportResponse ToResponse(this GradeReportView report) =>
        new(
            report.TotalQuestions,
            report.Correct,
            report.Incorrect,
            report.Unanswered,
            report.PendingReview,
            report.ScorePercent,
            report.Results
                .Select(r => new QuestionResultResponse(
                    r.QuestionId,
                    r.Type,
                    r.Outcome,
                    r.SelectedOptionIds.ToList(),
                    r.CorrectOptionIds.ToList(),
                    r.Score))
                .ToList());
}

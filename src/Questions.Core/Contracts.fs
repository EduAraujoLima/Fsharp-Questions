/// Contratos de fronteira entre o núcleo funcional (F#) e o mundo externo (C#).
/// São registros imutáveis compostos apenas de tipos primitivos, fáceis de construir
/// e consumir a partir de C#. Entradas são "não validadas"; saídas são "views".
namespace Questions.Core.Contracts

open System

// ---------------------------------- Entradas ----------------------------------------

type OptionInput = { Text: string; IsCorrect: bool }

type QuestionInput =
    { Type: string
      Statement: string
      Difficulty: string
      Options: OptionInput list }

type QuestionQuery =
    { Type: string
      Difficulty: string
      Search: string
      Page: int
      PageSize: int }

type ExamSectionInput =
    { Type: string
      Difficulty: string
      Count: int }

type ExamInput =
    { Title: string
      Sections: ExamSectionInput list
      ShuffleOptions: bool
      IncludeAnswerKey: bool }

type AnswerInput =
    { QuestionId: Guid
      SelectedOptionIds: int list
      Text: string }

type SubmissionInput = { Answers: AnswerInput list }

// ----------------------------- Snapshots e views ------------------------------------

/// Representação completa (com gabarito) de uma alternativa, usada para persistência e admin.
type OptionSnapshot =
    { Id: int
      Text: string
      IsCorrect: bool }

/// Representação completa de uma questão, usada para persistência e para o admin.
type QuestionSnapshot =
    { Id: Guid
      Type: string
      Statement: string
      Difficulty: string
      Options: OptionSnapshot list
      Version: int
      CreatedAt: DateTimeOffset
      UpdatedAt: DateTimeOffset }

type QuestionPage =
    { Items: QuestionSnapshot list
      Page: int
      PageSize: int
      TotalItems: int
      TotalPages: int }

type QuestionTypeInfo =
    { Code: string
      Description: string
      AcceptsOptions: bool
      MinOptions: int
      MaxOptions: int
      CorrectOptions: string }

type ExamOptionView =
    { Id: int
      Label: string
      Text: string }

type ExamQuestionView =
    { Number: int
      QuestionId: Guid
      Type: string
      Difficulty: string
      Statement: string
      Options: ExamOptionView list }

type AnswerKeyEntry =
    { Number: int
      QuestionId: Guid
      CorrectOptionIds: int list
      CorrectLabels: string list }

type ExamView =
    { Title: string
      Seed: int64
      GeneratedAt: DateTimeOffset
      TotalQuestions: int
      Questions: ExamQuestionView list
      IncludesAnswerKey: bool
      AnswerKey: AnswerKeyEntry list }

type QuestionResultView =
    { QuestionId: Guid
      Type: string
      Outcome: string
      SelectedOptionIds: int list
      CorrectOptionIds: int list
      Score: decimal }

type GradeReportView =
    { TotalQuestions: int
      Correct: int
      Incorrect: int
      Unanswered: int
      PendingReview: int
      ScorePercent: decimal
      Results: QuestionResultView list }

// ----------------------------------- Erros ------------------------------------------

/// Categoria do erro: o mundo externo decide como traduzi-la (ex.: status HTTP).
[<RequireQualifiedAccess>]
type ErrorKind =
    | InvalidInput
    | NotFound
    | Forbidden
    | BusinessRuleViolation
    | DataCorruption

type FieldError =
    { Field: string
      Code: string
      Message: string }

type ErrorView =
    { Kind: ErrorKind
      Code: string
      Message: string
      Errors: FieldError list }

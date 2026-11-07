/// Projeções do domínio para os contratos de saída consumidos pelo mundo externo.
module Questions.Core.Views

open Questions.Core.Contracts
open Questions.Core.QuestionCatalog
open Questions.Core.ExamGeneration
open Questions.Core.Grading

let questionPage (page: Page<Question>) : QuestionPage =
    { Items = page.Items |> List.map QuestionRules.toSnapshot
      Page = page.Page
      PageSize = page.PageSize
      TotalItems = page.TotalItems
      TotalPages = page.TotalPages }

let questionTypeInfo (questionType: QuestionType) : QuestionTypeInfo =
    let spec = QuestionType.optionsSpec questionType

    { Code = QuestionType.code questionType
      Description = QuestionType.describe questionType
      AcceptsOptions = Option.isSome spec
      MinOptions = spec |> Option.map _.Minimum |> Option.defaultValue 0
      MaxOptions = spec |> Option.map _.Maximum |> Option.defaultValue 0
      CorrectOptions =
        spec
        |> Option.map (fun s ->
            match s.CorrectOptions with
            | ExactlyOne -> "ExactlyOne"
            | AtLeastOne -> "AtLeastOne")
        |> Option.defaultValue "None" }

let private examQuestion (item: ExamItem) : ExamQuestionView =
    { Number = item.Number
      QuestionId = QuestionId.value item.Question.Id
      Type = item.Question |> Question.questionType |> QuestionType.code
      Difficulty = item.Question |> Question.difficulty |> Difficulty.code
      Statement = Question.statement item.Question
      Options =
        item.Options
        |> List.mapi (fun index option ->
            { Id = OptionId.value option.Id
              Label = OptionId.labelFor index
              Text = OptionText.value option.Text }) }

let private answerKeyEntry (item: ExamItem) : AnswerKeyEntry =
    let correct = item.Options |> List.indexed |> List.filter (snd >> _.IsCorrect)

    { Number = item.Number
      QuestionId = QuestionId.value item.Question.Id
      CorrectOptionIds = correct |> List.map (snd >> _.Id >> OptionId.value)
      CorrectLabels = correct |> List.map (fst >> OptionId.labelFor) }

let exam (exam: Exam) : ExamView =
    { Title = ExamTitle.value exam.Title
      Seed = exam.Seed
      GeneratedAt = exam.GeneratedAt
      TotalQuestions = List.length exam.Items
      Questions = exam.Items |> List.map examQuestion
      IncludesAnswerKey = exam.IncludesAnswerKey
      AnswerKey = if exam.IncludesAnswerKey then exam.Items |> List.map answerKeyEntry else [] }

let private sortedIds (ids: Set<OptionId>) = ids |> Set.toList |> List.map OptionId.value

let private questionResult (graded: GradedAnswer) : QuestionResultView =
    { QuestionId = QuestionId.value graded.Answer.Question.Id
      Type = graded.Answer.Question |> Question.questionType |> QuestionType.code
      Outcome = Outcome.code graded.Outcome
      SelectedOptionIds = sortedIds graded.Answer.Selected
      CorrectOptionIds = graded.Answer.Question |> Question.correctOptionIds |> sortedIds
      Score = graded.Score }

let gradeReport (report: GradeReport) : GradeReportView =
    { TotalQuestions = List.length report.Results
      Correct = report.Correct
      Incorrect = report.Incorrect
      Unanswered = report.Unanswered
      PendingReview = report.PendingReview
      ScorePercent = report.ScorePercent
      Results = report.Results |> List.map questionResult }

let private fieldError (error: ValidationError) : FieldError =
    { Field = ValidationError.field error
      Code = ValidationError.code error
      Message = ValidationError.message error }

let private errorKind =
    function
    | InvalidInput _ -> ErrorKind.InvalidInput
    | QuestionNotFound _ -> ErrorKind.NotFound
    | UnknownRole _
    | Forbidden _ -> ErrorKind.Forbidden
    | InsufficientQuestions _ -> ErrorKind.BusinessRuleViolation
    | CorruptedQuestion _ -> ErrorKind.DataCorruption

let error (appError: AppError) : ErrorView =
    let details =
        match appError with
        | InvalidInput errors
        | CorruptedQuestion(_, errors) -> errors |> List.map fieldError
        | _ -> []

    { Kind = errorKind appError
      Code = AppError.code appError
      Message = AppError.message appError
      Errors = details }

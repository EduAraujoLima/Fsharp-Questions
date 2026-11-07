/// Correção automática de respostas.
module Questions.Core.Grading

open System
open Questions.Core.Contracts

type Outcome =
    | Correct
    | Incorrect
    | Unanswered
    /// Questões dissertativas respondidas aguardam correção manual.
    | PendingReview

[<RequireQualifiedAccess>]
module Outcome =

    let code =
        function
        | Correct -> "Correct"
        | Incorrect -> "Incorrect"
        | Unanswered -> "Unanswered"
        | PendingReview -> "PendingReview"

    let score =
        function
        | Correct -> 1m
        | Incorrect
        | Unanswered
        | PendingReview -> 0m

type Answer =
    { Question: Question
      Selected: Set<OptionId>
      Text: string option }

type GradedAnswer =
    { Answer: Answer
      Outcome: Outcome
      Score: decimal }

type GradeReport =
    { Results: GradedAnswer list
      Correct: int
      Incorrect: int
      Unanswered: int
      PendingReview: int
      ScorePercent: decimal }

// ------------------------------- Validação ------------------------------------------

let private acceptsSingleOption question =
    match Question.questionType question with
    | QuestionType.SingleChoice
    | QuestionType.TrueFalse -> true
    | QuestionType.MultipleChoice
    | QuestionType.Essay -> false

let private validateSelection (question: Question) (selectedIds: int list) =
    let questionId = question.Id |> QuestionId.value
    let validIds = question |> Question.options |> List.map (_.Id >> OptionId.value) |> Set.ofList
    let distinct = List.distinct selectedIds

    let unknownOptions =
        distinct
        |> List.filter (validIds.Contains >> not)
        |> List.map (fun optionId -> UnknownOption(questionId, optionId))

    validation {
        let! () = if List.isEmpty unknownOptions then Validation.succeed () else Error unknownOptions

        and! () =
            Validation.require
                (SingleOptionExpected(questionId, List.length distinct))
                (not (acceptsSingleOption question) || List.length distinct <= 1)

        return distinct |> List.map OptionId |> Set.ofList
    }

let private validateAnswer (bank: Map<QuestionId, Question>) (index: int) (input: AnswerInput) =
    bank
    |> Map.tryFind (QuestionId input.QuestionId)
    |> Validation.ofOption (UnknownQuestion input.QuestionId)
    |> Result.bind (fun question ->
        input.SelectedOptionIds
        |> Option.ofObj
        |> Option.defaultValue []
        |> validateSelection question
        |> Validation.map (fun selected ->
            { Question = question
              Selected = selected
              Text = input.Text |> Option.ofObj |> Option.map _.Trim() |> Option.filter (String.IsNullOrEmpty >> not) }))
    |> Result.mapError (List.map (fun error -> InAnswer(index, error)))

let private requireUniqueQuestions (answers: AnswerInput list) =
    answers
    |> List.countBy _.QuestionId
    |> List.filter (snd >> (<) 1)
    |> List.map (fst >> DuplicateAnswer)
    |> function
        | [] -> Validation.succeed ()
        | errors -> Error errors

let validate (bank: Question list) (submission: SubmissionInput) : Validation<Answer list, ValidationError> =
    let byId = bank |> List.map (fun question -> question.Id, question) |> Map.ofList

    match submission.Answers |> Option.ofObj |> Option.defaultValue [] with
    | [] -> Validation.fail AnswersRequired
    | answers ->
        validation {
            let! validAnswers = answers |> Validation.traverseIndexed (validateAnswer byId)
            and! () = requireUniqueQuestions answers
            return validAnswers
        }

// ------------------------------- Correção -------------------------------------------

let evaluate (answer: Answer) : Outcome =
    match Question.questionType answer.Question with
    | QuestionType.Essay -> if Option.isSome answer.Text then PendingReview else Unanswered
    | _ when Set.isEmpty answer.Selected -> Unanswered
    | _ when answer.Selected = Question.correctOptionIds answer.Question -> Correct
    | _ -> Incorrect

let private grade (answer: Answer) : GradedAnswer =
    let outcome = evaluate answer

    { Answer = answer
      Outcome = outcome
      Score = Outcome.score outcome }

let private percentage (part: int) (whole: int) =
    if whole = 0 then 0m else Math.Round(decimal part * 100m / decimal whole, 2)

/// Corrige as respostas e consolida o relatório. A nota considera apenas questões objetivas.
let report (answers: Answer list) : GradeReport =
    let results = answers |> List.map grade
    let count outcome = results |> List.filter (_.Outcome >> (=) outcome) |> List.length
    let objective = results |> List.filter (_.Answer.Question >> Question.isObjective) |> List.length

    { Results = results
      Correct = count Correct
      Incorrect = count Incorrect
      Unanswered = count Unanswered
      PendingReview = count PendingReview
      ScorePercent = percentage (count Correct) objective }

/// Valida e corrige uma submissão.
let gradeSubmission (bank: Question list) : SubmissionInput -> Result<GradeReport, AppError> =
    validate bank >> Result.map report >> Result.mapError InvalidInput

/// Geração de provas aleatórias a partir do banco de questões.
/// Todo o processo é uma função pura de (semente, especificação, banco) -> prova:
/// a mesma semente sempre gera a mesma prova.
module Questions.Core.ExamGeneration

open System
open Questions.Core.Contracts

let maxSections = 20
let maxQuestionsPerExam = 100
let maxTitleLength = 200

type ExamTitle = private ExamTitle of string

[<RequireQualifiedAccess>]
module ExamTitle =

    let create (raw: string) : Validation<ExamTitle, ValidationError> =
        match raw |> Option.ofObj |> Option.map _.Trim() with
        | None
        | Some "" -> Validation.fail ExamTitleRequired
        | Some title when title.Length > maxTitleLength -> Validation.fail (ExamTitleTooLong maxTitleLength)
        | Some title -> Validation.succeed (ExamTitle title)

    let value (ExamTitle title) = title

/// Critério de uma seção da prova: "N questões do tipo T e dificuldade D" (T e D opcionais).
type SectionCriteria =
    { Type: QuestionType option
      Difficulty: Difficulty option
      Count: int }

type ExamBlueprint =
    { Title: ExamTitle
      Sections: SectionCriteria list
      ShuffleOptions: bool
      IncludeAnswerKey: bool }

/// Uma questão posicionada na prova, com as alternativas na ordem de exibição.
type ExamItem =
    { Number: int
      Question: Question
      Options: AnswerOption list }

type Exam =
    { Title: ExamTitle
      Seed: int64
      GeneratedAt: DateTimeOffset
      Items: ExamItem list
      IncludesAnswerKey: bool }

// ------------------------------- Validação ------------------------------------------

let private validateSection (index: int) (section: ExamSectionInput) : Validation<SectionCriteria, ValidationError> =
    validation {
        let! questionType = QuestionType.parseOptional section.Type
        and! difficulty = Difficulty.parseOptional section.Difficulty

        and! () =
            Validation.require
                (SectionCountOutOfRange(1, maxQuestionsPerExam, section.Count))
                (section.Count >= 1 && section.Count <= maxQuestionsPerExam)

        return
            { Type = questionType
              Difficulty = difficulty
              Count = section.Count }
    }
    |> Result.mapError (List.map (fun error -> InSection(index, error)))

let private validateSections (sections: ExamSectionInput list) : Validation<SectionCriteria list, ValidationError> =
    match sections |> Option.ofObj |> Option.defaultValue [] with
    | [] -> Validation.fail ExamSectionsRequired
    | tooMany when List.length tooMany > maxSections ->
        Validation.fail (TooManyExamSections(maxSections, List.length tooMany))
    | sections ->
        sections
        |> Validation.traverseIndexed validateSection
        |> Result.bind (fun criteria ->
            let total = criteria |> List.sumBy _.Count

            Validation.require (TotalQuestionsExceeded(maxQuestionsPerExam, total)) (total <= maxQuestionsPerExam)
            |> Validation.map (fun () -> criteria))

let validate (input: ExamInput) : Validation<ExamBlueprint, ValidationError> =
    validation {
        let! title = ExamTitle.create input.Title
        and! sections = validateSections input.Sections

        return
            { Title = title
              Sections = sections
              ShuffleOptions = input.ShuffleOptions
              IncludeAnswerKey = input.IncludeAnswerKey }
    }

// ------------------------------- Seleção --------------------------------------------

let describeCriteria (criteria: SectionCriteria) : string =
    let typePart =
        criteria.Type |> Option.map QuestionType.code |> Option.defaultValue "qualquer tipo"

    let difficultyPart =
        criteria.Difficulty |> Option.map Difficulty.code |> Option.defaultValue "qualquer dificuldade"

    $"{typePart} / {difficultyPart}"

let private matches (criteria: SectionCriteria) (question: Question) =
    criteria.Type |> Option.forall ((=) (Question.questionType question))
    && criteria.Difficulty |> Option.forall ((=) (Question.difficulty question))

/// Seções mais específicas são atendidas primeiro, para que uma seção genérica
/// ("qualquer tipo") não consuma questões necessárias a uma seção específica.
let private specificity (criteria: SectionCriteria) =
    [ Option.isSome criteria.Type; Option.isSome criteria.Difficulty ] |> List.filter id |> List.length

type private Selection =
    { Chosen: Question list
      Pool: Question list }

let private selectSection (criteria: SectionCriteria) (selection: Selection) : Rng<Result<Selection, AppError>> =
    let candidates, others = selection.Pool |> List.partition (matches criteria)
    let available = List.length candidates

    if available < criteria.Count then
        Rng.ret (Error(InsufficientQuestions(describeCriteria criteria, criteria.Count, available)))
    else
        candidates
        |> Rng.shuffle
        |> Rng.map (fun shuffled ->
            let picked, remaining = List.splitAt criteria.Count shuffled

            Ok
                { Chosen = selection.Chosen @ picked
                  Pool = remaining @ others })

/// Encadeia as seções: cada uma retira questões do que restou do banco (sem repetição).
let private selectAll (sections: SectionCriteria list) (bank: Question list) : Rng<Result<Question list, AppError>> =
    let step (state: Rng<Result<Selection, AppError>>) (criteria: SectionCriteria) =
        state
        |> Rng.bind (function
            | Ok selection -> selectSection criteria selection
            | Error error -> Rng.ret (Error error))

    sections
    |> List.sortByDescending specificity
    |> List.fold step (Rng.ret (Ok { Chosen = []; Pool = bank }))
    |> Rng.map (Result.map _.Chosen)

let private arrangeOptions (shuffleOptions: bool) (question: Question) : Rng<Question * AnswerOption list> =
    let options = Question.options question
    let arranged = if shuffleOptions then Rng.shuffle options else Rng.ret options
    arranged |> Rng.map (fun ordered -> question, ordered)

let private toItems (arranged: (Question * AnswerOption list) list) : ExamItem list =
    arranged
    |> List.mapi (fun index (question, options) ->
        { Number = index + 1
          Question = question
          Options = options })

/// Programa aleatório (puro) que monta os itens da prova.
let private examItems (blueprint: ExamBlueprint) (bank: Question list) : Rng<Result<ExamItem list, AppError>> =
    rng {
        // Ordena o banco para que o resultado dependa apenas da semente e do conteúdo,
        // e não da ordem em que o mundo externo entregou as questões.
        let! selection = bank |> List.sortBy (Question.id >> QuestionId.value) |> selectAll blueprint.Sections

        match selection with
        | Error error -> return Error error
        | Ok chosen ->
            let! ordered = Rng.shuffle chosen
            let! arranged = ordered |> Rng.traverse (arrangeOptions blueprint.ShuffleOptions)
            return Ok(toItems arranged)
    }

/// Gera a prova. Determinística: mesma semente + mesmo banco = mesma prova.
let generate
    (seed: int64)
    (generatedAt: DateTimeOffset)
    (blueprint: ExamBlueprint)
    (bank: Question list)
    : Result<Exam, AppError> =
    examItems blueprint bank
    |> Rng.eval (Seed.ofInt64 seed)
    |> Result.map (fun items ->
        { Title = blueprint.Title
          Seed = seed
          GeneratedAt = generatedAt
          Items = items
          IncludesAnswerKey = blueprint.IncludeAnswerKey })

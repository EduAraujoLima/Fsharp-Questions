/// Construtores de dados de teste.
module Questions.Core.Tests.Builders

open System
open Questions.Core
open Questions.Core.Contracts

let now = DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)

let guid (n: int) = Guid($"00000000-0000-0000-0000-%012d{n}")

let option (text: string) (isCorrect: bool) : OptionInput = { Text = text; IsCorrect = isCorrect }

let questionInput (questionType: string) (statement: string) (difficulty: string) (options: OptionInput list) : QuestionInput =
    { Type = questionType
      Statement = statement
      Difficulty = difficulty
      Options = options }

let singleChoice statement difficulty =
    questionInput "SingleChoice" statement difficulty [ option "A" true; option "B" false; option "C" false; option "D" false ]

let multipleChoice statement difficulty =
    questionInput "MultipleChoice" statement difficulty [ option "A" true; option "B" true; option "C" false ]

let trueFalse statement difficulty =
    questionInput "TrueFalse" statement difficulty [ option "Verdadeiro" false; option "Falso" true ]

let essay statement difficulty = questionInput "Essay" statement difficulty []

let createOrFail (id: Guid) (input: QuestionInput) : Question =
    match QuestionRules.create (QuestionId id) now input with
    | Ok question -> question
    | Error error -> failwithf "Questão de teste inválida: %A" error

/// Banco de exemplo: 5 SingleChoice (Easy), 3 MultipleChoice (Medium), 2 TrueFalse (Hard), 2 Essay (Medium).
let bank: Question list =
    [ yield! [ 1..5 ] |> List.map (fun n -> singleChoice $"Única {n}" "Easy")
      yield! [ 1..3 ] |> List.map (fun n -> multipleChoice $"Múltipla {n}" "Medium")
      yield! [ 1..2 ] |> List.map (fun n -> trueFalse $"VF {n}" "Hard")
      yield! [ 1..2 ] |> List.map (fun n -> essay $"Dissertativa {n}" "Medium") ]
    |> List.mapi (fun index input -> createOrFail (guid (index + 1)) input)

let bankSnapshots = bank |> List.map QuestionRules.toSnapshot

let section (questionType: string) (difficulty: string) (count: int) : ExamSectionInput =
    { Type = questionType
      Difficulty = difficulty
      Count = count }

let examInput (sections: ExamSectionInput list) : ExamInput =
    { Title = "Prova"
      Sections = sections
      ShuffleOptions = true
      IncludeAnswerKey = false }

/// Extrai os erros de validação de um resultado (falha o teste caso contrário).
let validationErrors (result: Result<'a, AppError>) : ValidationError list =
    match result with
    | Error(InvalidInput errors) -> errors
    | other -> failwithf "Esperava InvalidInput, obtido %A" other

let okOrFail (result: Result<'a, 'e>) : 'a =
    match result with
    | Ok value -> value
    | Error error -> failwithf "Esperava Ok, obtido %A" error

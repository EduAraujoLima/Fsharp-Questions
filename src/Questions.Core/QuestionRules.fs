/// Regras de negócio para cadastro, alteração e reidratação de questões.
module Questions.Core.QuestionRules

open System
open Questions.Core.Contracts

let private rawOptions (options: OptionInput list) =
    options |> Option.ofObj |> Option.defaultValue [] |> List.map (fun o -> o.Text, o.IsCorrect)

let private validateBody (rawType: string) (options: (string * bool) list) =
    // O corpo depende do tipo: se o tipo for inválido não faz sentido validar as alternativas.
    QuestionType.parse rawType |> Result.bind (fun questionType -> QuestionBody.create questionType options)

/// Valida o conteúdo de uma questão acumulando todos os erros (enunciado, tipo,
/// dificuldade e alternativas são verificados de forma independente).
let validateContent (input: QuestionInput) : Validation<QuestionContent, ValidationError> =
    validation {
        let! statement = Statement.create input.Statement
        and! difficulty = Difficulty.parseOrDefault input.Difficulty
        and! body = validateBody input.Type (rawOptions input.Options)

        return
            { Statement = statement
              Difficulty = difficulty
              Body = body }
    }

/// Cadastro: valida o conteúdo e cria a versão 1 da questão.
let create (id: QuestionId) (now: DateTimeOffset) : QuestionInput -> Result<Question, AppError> =
    validateContent >> Result.map (Question.create id now) >> Result.mapError InvalidInput

/// Alteração: valida o novo conteúdo e gera uma nova versão da questão existente.
let revise (now: DateTimeOffset) (input: QuestionInput) (existing: Question) : Result<Question, AppError> =
    validateContent input
    |> Result.map (fun content -> Question.revise now content existing)
    |> Result.mapError InvalidInput

/// Snapshot (dados persistidos) -> domínio. Os dados vindos de fora são revalidados:
/// o núcleo nunca confia cegamente no mundo externo.
let fromSnapshot (snapshot: QuestionSnapshot) : Result<Question, AppError> =
    let input: QuestionInput =
        { Type = snapshot.Type
          Statement = snapshot.Statement
          Difficulty = snapshot.Difficulty
          Options =
            snapshot.Options
            |> Option.ofObj
            |> Option.defaultValue []
            |> List.sortBy _.Id
            |> List.map (fun o -> { Text = o.Text; IsCorrect = o.IsCorrect }) }

    validateContent input
    |> Result.map (fun content ->
        { Id = QuestionId snapshot.Id
          Content = content
          Version = snapshot.Version
          CreatedAt = snapshot.CreatedAt
          UpdatedAt = snapshot.UpdatedAt })
    |> Result.mapError (fun errors -> CorruptedQuestion(snapshot.Id, errors))

/// Domínio -> snapshot (para persistência e para a visão do administrador).
let toSnapshot (question: Question) : QuestionSnapshot =
    { Id = QuestionId.value question.Id
      Type = question |> Question.questionType |> QuestionType.code
      Statement = Question.statement question
      Difficulty = question |> Question.difficulty |> Difficulty.code
      Options =
        question
        |> Question.options
        |> List.map (fun option ->
            { Id = OptionId.value option.Id
              Text = OptionText.value option.Text
              IsCorrect = option.IsCorrect })
      Version = question.Version
      CreatedAt = question.CreatedAt
      UpdatedAt = question.UpdatedAt }

/// Reidrata um banco de questões inteiro (falha na primeira questão corrompida).
let fromSnapshots (snapshots: QuestionSnapshot seq) : Result<Question list, AppError> =
    snapshots |> Option.ofObj |> Option.map List.ofSeq |> Option.defaultValue [] |> Result.traverse fromSnapshot

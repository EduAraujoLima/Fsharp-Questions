namespace Questions.Core

open System

// =====================================================================================
//  Modelo de domínio. Os tipos tornam estados inválidos irrepresentáveis:
//  - textos são "constrained types" com construtor privado (só nascem válidos);
//  - questões dissertativas simplesmente NÃO têm alternativas (não há lista vazia a checar);
//  - um conjunto de alternativas só existe se respeitar as regras do seu tipo de questão.
// =====================================================================================

/// Identificador de uma questão.
type QuestionId = QuestionId of Guid

/// Tipos de questão suportados.
[<RequireQualifiedAccess>]
type QuestionType =
    /// Múltipla escolha com exatamente uma alternativa correta.
    | SingleChoice
    /// Múltipla escolha com uma ou mais alternativas corretas.
    | MultipleChoice
    /// Verdadeiro ou falso: exatamente duas alternativas, uma correta.
    | TrueFalse
    /// Dissertativa: sem alternativas, correção manual.
    | Essay

[<RequireQualifiedAccess>]
type Difficulty =
    | Easy
    | Medium
    | Hard

/// Regra sobre quantas alternativas corretas uma questão objetiva deve ter.
type CorrectOptionsRule =
    | ExactlyOne
    | AtLeastOne

/// Especificação das alternativas de um tipo de questão objetiva.
type OptionsSpec =
    { Minimum: int
      Maximum: int
      CorrectOptions: CorrectOptionsRule }

/// Enunciado não vazio e de tamanho limitado.
type Statement = private Statement of string

/// Texto de alternativa não vazio e de tamanho limitado.
type OptionText = private OptionText of string

/// Identificador de uma alternativa dentro da questão (posição original, base 1).
type OptionId = OptionId of int

type AnswerOption =
    { Id: OptionId
      Text: OptionText
      IsCorrect: bool }

/// Conjunto de alternativas já validado segundo a especificação do tipo da questão.
type Alternatives = private Alternatives of AnswerOption list

/// O "corpo" da questão: a forma muda conforme o tipo.
[<RequireQualifiedAccess>]
type QuestionBody =
    | SingleChoice of Alternatives
    | MultipleChoice of Alternatives
    | TrueFalse of Alternatives
    | Essay

/// O conteúdo editável de uma questão.
type QuestionContent =
    { Statement: Statement
      Difficulty: Difficulty
      Body: QuestionBody }

/// Uma questão do banco.
type Question =
    { Id: QuestionId
      Content: QuestionContent
      Version: int
      CreatedAt: DateTimeOffset
      UpdatedAt: DateTimeOffset }

// -------------------------------------------------------------------------------------

/// Funções auxiliares para tipos textuais restritos.
module private Text =

    let normalize (raw: string) : string option =
        raw |> Option.ofObj |> Option.map _.Trim() |> Option.filter (String.IsNullOrEmpty >> not)

    let caseInsensitiveEquals (a: string) (b: string) =
        String.Equals(a, b, StringComparison.OrdinalIgnoreCase)

[<RequireQualifiedAccess>]
module QuestionId =

    let value (QuestionId id) = id

[<RequireQualifiedAccess>]
module QuestionType =

    let all =
        [ QuestionType.SingleChoice
          QuestionType.MultipleChoice
          QuestionType.TrueFalse
          QuestionType.Essay ]

    let code =
        function
        | QuestionType.SingleChoice -> "SingleChoice"
        | QuestionType.MultipleChoice -> "MultipleChoice"
        | QuestionType.TrueFalse -> "TrueFalse"
        | QuestionType.Essay -> "Essay"

    let describe =
        function
        | QuestionType.SingleChoice -> "Múltipla escolha com uma única alternativa correta"
        | QuestionType.MultipleChoice -> "Múltipla escolha com uma ou mais alternativas corretas"
        | QuestionType.TrueFalse -> "Verdadeiro ou falso"
        | QuestionType.Essay -> "Dissertativa (sem alternativas, correção manual)"

    /// Regras de alternativas por tipo; None = o tipo não aceita alternativas.
    let optionsSpec =
        function
        | QuestionType.SingleChoice ->
            Some
                { Minimum = 2
                  Maximum = 10
                  CorrectOptions = ExactlyOne }
        | QuestionType.MultipleChoice ->
            Some
                { Minimum = 2
                  Maximum = 10
                  CorrectOptions = AtLeastOne }
        | QuestionType.TrueFalse ->
            Some
                { Minimum = 2
                  Maximum = 2
                  CorrectOptions = ExactlyOne }
        | QuestionType.Essay -> None

    let tryParse (raw: string) : QuestionType option =
        Text.normalize raw
        |> Option.bind (fun value -> all |> List.tryFind (code >> Text.caseInsensitiveEquals value))

    let parse (raw: string) : Validation<QuestionType, ValidationError> =
        match Text.normalize raw with
        | None -> Validation.fail QuestionTypeRequired
        | Some value -> tryParse value |> Validation.ofOption (UnknownQuestionType value)

    /// Interpreta um filtro opcional: vazio significa "qualquer tipo".
    let parseOptional (raw: string) : Validation<QuestionType option, ValidationError> =
        match Text.normalize raw with
        | None -> Validation.succeed None
        | Some value -> tryParse value |> Option.map Some |> Validation.ofOption (UnknownQuestionType value)

[<RequireQualifiedAccess>]
module Difficulty =

    let all = [ Difficulty.Easy; Difficulty.Medium; Difficulty.Hard ]

    let defaultValue = Difficulty.Medium

    let code =
        function
        | Difficulty.Easy -> "Easy"
        | Difficulty.Medium -> "Medium"
        | Difficulty.Hard -> "Hard"

    let tryParse (raw: string) : Difficulty option =
        Text.normalize raw
        |> Option.bind (fun value -> all |> List.tryFind (code >> Text.caseInsensitiveEquals value))

    /// Interpreta um filtro opcional: vazio significa "qualquer dificuldade".
    let parseOptional (raw: string) : Validation<Difficulty option, ValidationError> =
        match Text.normalize raw with
        | None -> Validation.succeed None
        | Some value -> tryParse value |> Option.map Some |> Validation.ofOption (UnknownDifficulty value)

    /// Dificuldade de uma questão; quando omitida assume o valor padrão (Medium).
    let parseOrDefault (raw: string) : Validation<Difficulty, ValidationError> =
        parseOptional raw |> Validation.map (Option.defaultValue defaultValue)

[<RequireQualifiedAccess>]
module Statement =

    let maxLength = 4000

    let create (raw: string) : Validation<Statement, ValidationError> =
        match Text.normalize raw with
        | None -> Validation.fail StatementRequired
        | Some value when value.Length > maxLength -> Validation.fail (StatementTooLong maxLength)
        | Some value -> Validation.succeed (Statement value)

    let value (Statement value) = value

[<RequireQualifiedAccess>]
module OptionText =

    let maxLength = 1000

    /// `position` é a posição (base 1) da alternativa, usada nas mensagens de erro.
    let create (position: int) (raw: string) : Validation<OptionText, ValidationError> =
        match Text.normalize raw with
        | None -> Validation.fail (OptionTextRequired position)
        | Some value when value.Length > maxLength -> Validation.fail (OptionTextTooLong(position, maxLength))
        | Some value -> Validation.succeed (OptionText value)

    let value (OptionText value) = value

[<RequireQualifiedAccess>]
module OptionId =

    let value (OptionId id) = id

    /// Rótulo de exibição para uma posição (0 -> "A", 1 -> "B", ...).
    let labelFor (index: int) = string (char (int 'A' + index))

[<RequireQualifiedAccess>]
module Alternatives =

    let value (Alternatives options) = options

    let ids: Alternatives -> Set<OptionId> = value >> List.map _.Id >> Set.ofList

    let correctIds: Alternatives -> Set<OptionId> =
        value >> List.filter _.IsCorrect >> List.map _.Id >> Set.ofList

    let private requireCount (spec: OptionsSpec) (count: int) =
        Validation.all
            [ Validation.require (TooFewOptions(spec.Minimum, count)) (count >= spec.Minimum)
              Validation.require (TooManyOptions(spec.Maximum, count)) (count <= spec.Maximum) ]

    let private requireCorrect (spec: OptionsSpec) (correctCount: int) =
        match spec.CorrectOptions with
        | ExactlyOne -> Validation.require (ExactlyOneCorrectOptionRequired correctCount) (correctCount = 1)
        | AtLeastOne -> Validation.require AtLeastOneCorrectOptionRequired (correctCount >= 1)

    let private requireDistinct (texts: string list) =
        texts
        |> List.choose Text.normalize
        |> List.groupBy _.ToUpperInvariant()
        |> List.filter (snd >> List.length >> (<) 1)
        |> List.map (snd >> List.head >> DuplicateOption)
        |> function
            | [] -> Validation.succeed ()
            | errors -> Error errors

    let private createOption (index: int) (text: string, isCorrect: bool) =
        OptionText.create (index + 1) text
        |> Validation.map (fun optionText ->
            { Id = OptionId(index + 1)
              Text = optionText
              IsCorrect = isCorrect })

    /// Valida as alternativas (texto, correta?) contra a especificação do tipo,
    /// acumulando todos os erros encontrados.
    let create (spec: OptionsSpec) (raw: (string * bool) list) : Validation<Alternatives, ValidationError> =
        validation {
            let! options = raw |> Validation.traverseIndexed createOption
            and! () = requireCount spec (List.length raw)
            and! () = requireCorrect spec (raw |> List.filter snd |> List.length)
            and! () = requireDistinct (raw |> List.map fst)
            return Alternatives options
        }

[<RequireQualifiedAccess>]
module QuestionBody =

    let questionType =
        function
        | QuestionBody.SingleChoice _ -> QuestionType.SingleChoice
        | QuestionBody.MultipleChoice _ -> QuestionType.MultipleChoice
        | QuestionBody.TrueFalse _ -> QuestionType.TrueFalse
        | QuestionBody.Essay -> QuestionType.Essay

    let alternatives =
        function
        | QuestionBody.SingleChoice alternatives
        | QuestionBody.MultipleChoice alternatives
        | QuestionBody.TrueFalse alternatives -> Some alternatives
        | QuestionBody.Essay -> None

    let options: QuestionBody -> AnswerOption list =
        alternatives >> Option.map Alternatives.value >> Option.defaultValue []

    let private wrap questionType : Alternatives -> QuestionBody =
        match questionType with
        | QuestionType.SingleChoice -> QuestionBody.SingleChoice
        | QuestionType.MultipleChoice -> QuestionBody.MultipleChoice
        | QuestionType.TrueFalse -> QuestionBody.TrueFalse
        | QuestionType.Essay -> fun _ -> QuestionBody.Essay

    /// Constrói o corpo da questão de acordo com as regras do tipo.
    let create (questionType: QuestionType) (raw: (string * bool) list) : Validation<QuestionBody, ValidationError> =
        match QuestionType.optionsSpec questionType with
        | None ->
            Validation.require (OptionsNotAllowed(QuestionType.code questionType)) (List.isEmpty raw)
            |> Validation.map (fun () -> QuestionBody.Essay)
        | Some spec -> Alternatives.create spec raw |> Validation.map (wrap questionType)

[<RequireQualifiedAccess>]
module Question =

    let id (question: Question) = question.Id

    let questionType (question: Question) = QuestionBody.questionType question.Content.Body

    let difficulty (question: Question) = question.Content.Difficulty

    let statement (question: Question) = Statement.value question.Content.Statement

    let options (question: Question) = QuestionBody.options question.Content.Body

    /// Alternativas corretas (vazio para dissertativas).
    let correctOptionIds (question: Question) : Set<OptionId> =
        question.Content.Body
        |> QuestionBody.alternatives
        |> Option.map Alternatives.correctIds
        |> Option.defaultValue Set.empty

    let isObjective (question: Question) = questionType question <> QuestionType.Essay

    /// Nova questão (versão 1).
    let create (id: QuestionId) (now: DateTimeOffset) (content: QuestionContent) : Question =
        { Id = id
          Content = content
          Version = 1
          CreatedAt = now
          UpdatedAt = now }

    /// Nova versão imutável da questão com o conteúdo substituído.
    let revise (now: DateTimeOffset) (content: QuestionContent) (question: Question) : Question =
        { question with
            Content = content
            Version = question.Version + 1
            UpdatedAt = now }

module Questions.Core.Tests.QuestionRulesTests

open Xunit
open Questions.Core
open Questions.Core.Contracts
open Questions.Core.Tests.Builders

let private create = QuestionRules.create (QuestionId(guid 1)) now

[<Fact>]
let ``cria questão de escolha única válida`` () =
    let question = singleChoice "  Quanto é 2 + 2?  " "Hard" |> create |> okOrFail

    Assert.Equal(QuestionType.SingleChoice, Question.questionType question)
    Assert.Equal("Quanto é 2 + 2?", Question.statement question)
    Assert.Equal(Difficulty.Hard, Question.difficulty question)
    Assert.Equal(1, question.Version)
    Assert.Equal<Set<OptionId>>(Set.ofList [ OptionId 1 ], Question.correctOptionIds question)

[<Fact>]
let ``dificuldade omitida assume Medium e tipo é case-insensitive`` () =
    let question = questionInput "truefalse" "Céu é azul" null [ option "V" true; option "F" false ] |> create |> okOrFail

    Assert.Equal(Difficulty.Medium, Question.difficulty question)
    Assert.Equal(QuestionType.TrueFalse, Question.questionType question)

[<Fact>]
let ``acumula todos os erros de validação em vez de parar no primeiro`` () =
    let errors =
        questionInput "SingleChoice" "   " "Impossível" [ option "a" true; option " A " true; option "" false ]
        |> create
        |> validationErrors

    Assert.Contains(StatementRequired, errors)
    Assert.Contains(UnknownDifficulty "Impossível", errors)
    Assert.Contains(OptionTextRequired 3, errors)
    Assert.Contains(ExactlyOneCorrectOptionRequired 2, errors)
    Assert.Contains(DuplicateOption "a", errors)
    Assert.Equal(5, List.length errors)

[<Fact>]
let ``tipo desconhecido ou ausente é rejeitado`` () =
    Assert.Equal<ValidationError list>([ UnknownQuestionType "Quiz" ], questionInput "Quiz" "x" "Easy" [] |> create |> validationErrors)
    Assert.Equal<ValidationError list>([ QuestionTypeRequired ], questionInput "" "x" "Easy" [] |> create |> validationErrors)

[<Fact>]
let ``dissertativa não aceita alternativas`` () =
    let errors = questionInput "Essay" "Explique" "Easy" [ option "x" true ] |> create |> validationErrors
    Assert.Equal<ValidationError list>([ OptionsNotAllowed "Essay" ], errors)

[<Fact>]
let ``dissertativa sem alternativas é válida`` () =
    let question = essay "Explique monads" "Hard" |> create |> okOrFail
    Assert.Equal(QuestionBody.Essay, question.Content.Body)
    Assert.Empty(Question.options question)

[<Fact>]
let ``verdadeiro ou falso exige exatamente duas alternativas`` () =
    let errors =
        questionInput "TrueFalse" "x" "Easy" [ option "V" true; option "F" false; option "Talvez" false ]
        |> create
        |> validationErrors

    Assert.Equal<ValidationError list>([ TooManyOptions(2, 3) ], errors)

[<Fact>]
let ``múltipla escolha exige ao menos uma correta e aceita várias`` () =
    let errors = questionInput "MultipleChoice" "x" "Easy" [ option "A" false; option "B" false ] |> create |> validationErrors
    Assert.Equal<ValidationError list>([ AtLeastOneCorrectOptionRequired ], errors)

    let question = multipleChoice "x" "Easy" |> create |> okOrFail
    Assert.Equal<Set<OptionId>>(Set.ofList [ OptionId 1; OptionId 2 ], Question.correctOptionIds question)

[<Fact>]
let ``escolha única exige pelo menos duas alternativas`` () =
    let errors = questionInput "SingleChoice" "x" "Easy" [ option "A" true ] |> create |> validationErrors
    Assert.Equal<ValidationError list>([ TooFewOptions(2, 1) ], errors)

[<Fact>]
let ``revisão gera nova versão preservando id e data de criação`` () =
    let original = singleChoice "Original" "Easy" |> create |> okOrFail
    let later = now.AddDays 1.0

    let revised = QuestionRules.revise later (essay "Revisada" "Hard") original |> okOrFail

    Assert.Equal(original.Id, revised.Id)
    Assert.Equal(original.CreatedAt, revised.CreatedAt)
    Assert.Equal(later, revised.UpdatedAt)
    Assert.Equal(2, revised.Version)
    Assert.Equal(QuestionType.Essay, Question.questionType revised)
    // A questão original não foi alterada (imutabilidade).
    Assert.Equal(1, original.Version)
    Assert.Equal("Original", Question.statement original)

[<Fact>]
let ``snapshot faz ida e volta sem perda`` () =
    bank
    |> List.iter (fun question ->
        let roundTrip = question |> QuestionRules.toSnapshot |> QuestionRules.fromSnapshot |> okOrFail
        Assert.Equal(question, roundTrip))

[<Fact>]
let ``snapshot corrompido é detectado`` () =
    let snapshot = { QuestionRules.toSnapshot bank.Head with Statement = "" }

    match QuestionRules.fromSnapshot snapshot with
    | Error(CorruptedQuestion(id, [ StatementRequired ])) -> Assert.Equal(snapshot.Id, id)
    | other -> failwithf "Esperava CorruptedQuestion, obtido %A" other

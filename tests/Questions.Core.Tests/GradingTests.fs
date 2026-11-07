module Questions.Core.Tests.GradingTests

open Xunit
open Questions.Core
open Questions.Core.Contracts
open Questions.Core.Grading
open Questions.Core.Tests.Builders

let private single = createOrFail (guid 101) (singleChoice "Única" "Easy") // correta: 1
let private multiple = createOrFail (guid 102) (multipleChoice "Múltipla" "Easy") // corretas: 1 e 2
let private tf = createOrFail (guid 103) (trueFalse "VF" "Easy") // correta: 2
let private written = createOrFail (guid 104) (essay "Dissertativa" "Easy")
let private questions = [ single; multiple; tf; written ]

let private answer (question: Question) (selected: int list) (text: string) : AnswerInput =
    { QuestionId = QuestionId.value question.Id
      SelectedOptionIds = selected
      Text = text }

let private grade answers =
    gradeSubmission questions { Answers = answers }

[<Fact>]
let ``corrige cada tipo de questão`` () =
    let report =
        grade [ answer single [ 1 ] null; answer multiple [ 1 ] null; answer tf [] null; answer written [] "Resposta" ]
        |> okOrFail

    let outcomes = report.Results |> List.map _.Outcome
    Assert.Equal<Outcome list>([ Correct; Incorrect; Unanswered; PendingReview ], outcomes)
    Assert.Equal(1, report.Correct)
    Assert.Equal(1, report.Incorrect)
    Assert.Equal(1, report.Unanswered)
    Assert.Equal(1, report.PendingReview)
    // 1 acerto entre 3 questões objetivas.
    Assert.Equal(33.33m, report.ScorePercent)

[<Fact>]
let ``múltipla escolha só é correta com o conjunto exato`` () =
    let report = grade [ answer multiple [ 2; 1 ] null ] |> okOrFail
    Assert.Equal(Correct, report.Results.Head.Outcome)
    Assert.Equal(100m, report.ScorePercent)

[<Fact>]
let ``dissertativa em branco conta como não respondida`` () =
    let report = grade [ answer written [] "   " ] |> okOrFail
    Assert.Equal(Unanswered, report.Results.Head.Outcome)
    Assert.Equal(0m, report.ScorePercent)

[<Fact>]
let ``valida a submissão acumulando erros`` () =
    let unknown = guid 999

    let errors =
        grade
            [ answer single [ 1; 2 ] null
              answer tf [ 7 ] null
              { QuestionId = unknown; SelectedOptionIds = []; Text = null }
              answer tf [ 1 ] null ]
        |> validationErrors

    Assert.Contains(InAnswer(0, SingleOptionExpected(QuestionId.value single.Id, 2)), errors)
    Assert.Contains(InAnswer(1, UnknownOption(QuestionId.value tf.Id, 7)), errors)
    Assert.Contains(InAnswer(2, UnknownQuestion unknown), errors)
    Assert.Contains(DuplicateAnswer(QuestionId.value tf.Id), errors)

[<Fact>]
let ``submissão vazia é rejeitada`` () =
    Assert.Equal<ValidationError list>([ AnswersRequired ], grade [] |> validationErrors)

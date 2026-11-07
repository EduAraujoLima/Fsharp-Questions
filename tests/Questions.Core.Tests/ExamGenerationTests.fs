module Questions.Core.Tests.ExamGenerationTests

open Xunit
open FsCheck.Xunit
open Questions.Core
open Questions.Core.ExamGeneration
open Questions.Core.Tests.Builders

let private generateWith (seed: int64) (input: Contracts.ExamInput) (questions: Question list) =
    validate input
    |> Result.mapError InvalidInput
    |> Result.bind (fun blueprint -> generate seed now blueprint questions)

let private questionIds (exam: Exam) = exam.Items |> List.map (_.Question.Id)

[<Fact>]
let ``valida título, seções e quantidades acumulando erros`` () =
    let errors =
        { examInput [ section "Quiz" "Easy" 0; section "" "Muito difícil" 1 ] with Title = " " }
        |> validate
        |> Result.mapError InvalidInput
        |> validationErrors

    Assert.Contains(ExamTitleRequired, errors)
    Assert.Contains(InSection(0, UnknownQuestionType "Quiz"), errors)
    Assert.Contains(InSection(0, SectionCountOutOfRange(1, maxQuestionsPerExam, 0)), errors)
    Assert.Contains(InSection(1, UnknownDifficulty "Muito difícil"), errors)

[<Fact>]
let ``exige ao menos uma seção e limita o total de questões`` () =
    let noSections = examInput [] |> validate |> Result.mapError InvalidInput |> validationErrors
    Assert.Equal<ValidationError list>([ ExamSectionsRequired ], noSections)

    let tooMany = examInput [ section "" "" 60; section "" "" 60 ] |> validate |> Result.mapError InvalidInput |> validationErrors
    Assert.Equal<ValidationError list>([ TotalQuestionsExceeded(maxQuestionsPerExam, 120) ], tooMany)

[<Fact>]
let ``respeita a composição pedida por tipo e dificuldade`` () =
    let exam =
        generateWith 7L (examInput [ section "SingleChoice" "" 3; section "TrueFalse" "Hard" 2; section "Essay" "" 1 ]) bank
        |> okOrFail

    let countOf questionType =
        exam.Items |> List.filter (_.Question >> Question.questionType >> (=) questionType) |> List.length

    Assert.Equal(6, exam.Items.Length)
    Assert.Equal(3, countOf QuestionType.SingleChoice)
    Assert.Equal(2, countOf QuestionType.TrueFalse)
    Assert.Equal(1, countOf QuestionType.Essay)
    Assert.Equal<int list>([ 1..6 ], exam.Items |> List.map _.Number)

[<Property>]
let ``nunca repete questões e sempre atende a quantidade`` (seed: int64) =
    let exam = generateWith seed (examInput [ section "" "" 5; section "MultipleChoice" "" 3 ]) bank |> okOrFail
    let ids = questionIds exam
    List.length ids = 8 && List.distinct ids = ids

[<Property>]
let ``mesma semente gera a mesma prova, independentemente da ordem do banco`` (seed: int64) =
    let input = examInput [ section "" "" 6 ]
    let first = generateWith seed input bank |> okOrFail
    let second = generateWith seed input (List.rev bank) |> okOrFail
    first = second

[<Fact>]
let ``sementes diferentes produzem provas diferentes`` () =
    let input = examInput [ section "" "" 10 ]
    let exams = [ 1L..10L ] |> List.map (fun seed -> generateWith seed input bank |> okOrFail |> questionIds)
    Assert.True(List.length (List.distinct exams) > 1)

[<Fact>]
let ``falha quando não há questões suficientes`` () =
    match generateWith 1L (examInput [ section "Essay" "" 3 ]) bank with
    | Error(InsufficientQuestions(criteria, requested, available)) ->
        Assert.Equal("Essay / qualquer dificuldade", criteria)
        Assert.Equal(3, requested)
        Assert.Equal(2, available)
    | other -> failwithf "Esperava InsufficientQuestions, obtido %A" other

[<Property>]
let ``seções específicas são atendidas antes das genéricas`` (seed: int64) =
    // Usa o banco inteiro: se a seção genérica fosse atendida primeiro poderia consumir as dissertativas.
    generateWith seed (examInput [ section "" "" 10; section "Essay" "" 2 ]) bank |> Result.isOk

[<Property>]
let ``embaralhar alternativas preserva ids e gabarito`` (seed: int64) =
    let exam = generateWith seed (examInput [ section "" "" 12 ]) bank |> okOrFail

    let byId (options: AnswerOption list) = options |> List.sortBy (fun option -> option.Id)

    exam.Items |> List.forall (fun item -> byId item.Options = byId (Question.options item.Question))

[<Fact>]
let ``sem embaralhar, alternativas mantêm a ordem original`` () =
    let exam =
        generateWith 3L { examInput [ section "SingleChoice" "" 5 ] with ShuffleOptions = false } bank |> okOrFail

    exam.Items |> List.iter (fun item -> Assert.Equal<AnswerOption list>(Question.options item.Question, item.Options))

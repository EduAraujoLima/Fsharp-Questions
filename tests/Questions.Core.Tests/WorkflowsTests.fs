module Questions.Core.Tests.WorkflowsTests

open Xunit
open Questions.Core
open Questions.Core.Contracts
open Questions.Core.Tests.Builders

let private errorOf (result: Result<'a, ErrorView>) =
    match result with
    | Error error -> error
    | Ok value -> failwithf "Esperava erro, obtido %A" value

let private query: QuestionQuery =
    { Type = null
      Difficulty = null
      Search = null
      Page = 1
      PageSize = 20 }

[<Fact>]
let ``somente Admin pode cadastrar questões`` () =
    let created = Workflows.createQuestion "Admin" (guid 1) now (singleChoice "Q" "Easy") |> okOrFail
    Assert.Equal(guid 1, created.Id)

    let denied = Workflows.createQuestion "Student" (guid 1) now (singleChoice "Q" "Easy") |> errorOf
    Assert.Equal(ErrorKind.Forbidden, denied.Kind)

    let unknown = Workflows.createQuestion "Hacker" (guid 1) now (singleChoice "Q" "Easy") |> errorOf
    Assert.Equal("unknown_role", unknown.Code)

[<Fact>]
let ``erros de validação viram FieldErrors com caminho do campo`` () =
    let error =
        Workflows.createQuestion "Admin" (guid 1) now (questionInput "SingleChoice" "" "Easy" [ option "" true; option "B" false ])
        |> errorOf

    Assert.Equal(ErrorKind.InvalidInput, error.Kind)
    Assert.Equal<string list>([ "statement"; "options[0].text" ], error.Errors |> List.map _.Field)

[<Fact>]
let ``alterar questão inexistente retorna NotFound`` () =
    let error = Workflows.updateQuestion "Admin" now (guid 5) (essay "x" "Easy") None |> errorOf
    Assert.Equal(ErrorKind.NotFound, error.Kind)

[<Fact>]
let ``excluir exige permissão e existência`` () =
    let existing = Some bankSnapshots.Head
    Assert.Equal(bankSnapshots.Head.Id, Workflows.deleteQuestion "Admin" bankSnapshots.Head.Id existing |> okOrFail)
    Assert.Equal(ErrorKind.Forbidden, (Workflows.deleteQuestion "Student" bankSnapshots.Head.Id existing |> errorOf).Kind)
    Assert.Equal(ErrorKind.NotFound, (Workflows.deleteQuestion "Admin" (guid 999) None |> errorOf).Kind)

[<Fact>]
let ``listagem filtra, ordena e pagina`` () =
    let run = Workflows.listQuestions "Admin" { query with Type = "SingleChoice"; PageSize = 2; Page = 3 } |> okOrFail
    let page = run bankSnapshots |> okOrFail

    Assert.Equal(5, page.TotalItems)
    Assert.Equal(3, page.TotalPages)
    Assert.Single(page.Items) |> ignore

    let searched = Workflows.listQuestions "Admin" { query with Search = "dissertativa" } |> okOrFail
    Assert.Equal(2, (searched bankSnapshots |> okOrFail).TotalItems)

[<Fact>]
let ``listagem é negada ao aluno antes de carregar dados`` () =
    let error = Workflows.listQuestions "Student" query |> errorOf
    Assert.Equal(ErrorKind.Forbidden, error.Kind)

[<Fact>]
let ``consulta inválida é rejeitada no primeiro estágio`` () =
    let error = Workflows.listQuestions "Admin" { query with Page = 0; PageSize = 500; Type = "X" } |> errorOf
    Assert.Equal<string list>([ "type"; "page"; "pageSize" ], error.Errors |> List.map _.Field)

[<Fact>]
let ``gabarito só para quem tem permissão`` () =
    let input = { examInput [ section "SingleChoice" "" 2 ] with IncludeAnswerKey = true }

    let denied = Workflows.generateExam "Student" 1L now input |> errorOf
    Assert.Equal(ErrorKind.Forbidden, denied.Kind)

    let exam = (Workflows.generateExam "Admin" 1L now input |> okOrFail) bankSnapshots |> okOrFail
    Assert.True(exam.IncludesAnswerKey)
    Assert.Equal(2, exam.AnswerKey.Length)

    exam.AnswerKey
    |> List.iter (fun key ->
        let question = exam.Questions |> List.find (fun q -> q.QuestionId = key.QuestionId)
        // Na escolha única do banco de teste a alternativa correta é sempre a de id 1.
        Assert.Equal<int list>([ 1 ], key.CorrectOptionIds)
        let label = question.Options |> List.find (fun o -> o.Id = 1) |> _.Label
        Assert.Equal<string list>([ label ], key.CorrectLabels))

[<Fact>]
let ``prova do aluno não traz gabarito`` () =
    let exam =
        (Workflows.generateExam "Student" 9L now (examInput [ section "" "" 4 ]) |> okOrFail) bankSnapshots
        |> okOrFail

    Assert.False(exam.IncludesAnswerKey)
    Assert.Empty(exam.AnswerKey)
    Assert.Equal(4, exam.TotalQuestions)

[<Fact>]
let ``tipos de questão expõem suas regras`` () =
    let codes = Workflows.questionTypes |> List.map _.Code
    Assert.Equal<string list>([ "SingleChoice"; "MultipleChoice"; "TrueFalse"; "Essay" ], codes)

    let trueFalseInfo = Workflows.questionTypes |> List.find (_.Code >> (=) "TrueFalse")
    Assert.Equal(2, trueFalseInfo.MinOptions)
    Assert.Equal(2, trueFalseInfo.MaxOptions)

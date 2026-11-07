/// Casos de uso expostos ao mundo externo (fachada consumida pelo C#).
///
/// Cada caso de uso é uma função PURA: recebe tudo de que precisa (papel do usuário,
/// identificador gerado, data/hora atual, semente aleatória, dados já carregados) e
/// devolve um Result com a decisão. Quem efetua I/O (banco de dados, relógio, HTTP)
/// é o shell imperativo em C# — o padrão "functional core, imperative shell".
module Questions.Core.Workflows

open System
open Questions.Core.Contracts

/// Segundo estágio de um caso de uso: recebe o banco de questões (carregado pelo mundo
/// externo) e produz o resultado final.
type WithBank<'a> = QuestionSnapshot seq -> Result<'a, ErrorView>

let private present (view: 'a -> 'b) : Result<'a, AppError> -> Result<'b, ErrorView> =
    Result.map view >> Result.mapError Views.error

let private ensureFound (questionId: Guid) (existing: QuestionSnapshot option) : Result<QuestionSnapshot, AppError> =
    existing
    |> Option.filter (fun snapshot -> snapshot.Id = questionId)
    |> Result.ofOption (QuestionNotFound questionId)

let private loadExisting (questionId: Guid) : QuestionSnapshot option -> Result<Question, AppError> =
    ensureFound questionId >> Result.bind QuestionRules.fromSnapshot

/// Tipos de questão suportados e suas regras.
[<CompiledName("QuestionTypes")>]
let questionTypes: QuestionTypeInfo list = QuestionType.all |> List.map Views.questionTypeInfo

/// Cadastra uma questão (somente Admin).
[<CompiledName("CreateQuestion")>]
let createQuestion
    (role: string)
    (questionId: Guid)
    (now: DateTimeOffset)
    (input: QuestionInput)
    : Result<QuestionSnapshot, ErrorView> =
    result {
        let! _ = Authorization.authorize Permission.ManageQuestions role
        return! QuestionRules.create (QuestionId questionId) now input
    }
    |> present QuestionRules.toSnapshot

/// Altera uma questão existente gerando uma nova versão (somente Admin).
[<CompiledName("UpdateQuestion")>]
let updateQuestion
    (role: string)
    (now: DateTimeOffset)
    (questionId: Guid)
    (input: QuestionInput)
    (existing: QuestionSnapshot option)
    : Result<QuestionSnapshot, ErrorView> =
    result {
        let! _ = Authorization.authorize Permission.ManageQuestions role
        let! current = loadExisting questionId existing
        return! QuestionRules.revise now input current
    }
    |> present QuestionRules.toSnapshot

/// Decide se a exclusão é permitida; devolve o id a ser removido (somente Admin).
[<CompiledName("DeleteQuestion")>]
let deleteQuestion (role: string) (questionId: Guid) (existing: QuestionSnapshot option) : Result<Guid, ErrorView> =
    result {
        let! _ = Authorization.authorize Permission.ManageQuestions role
        let! found = ensureFound questionId existing
        return found.Id
    }
    |> present id

/// Detalhe de uma questão com gabarito (somente Admin).
[<CompiledName("GetQuestion")>]
let getQuestion (role: string) (questionId: Guid) (existing: QuestionSnapshot option) : Result<QuestionSnapshot, ErrorView> =
    result {
        let! _ = Authorization.authorize Permission.ViewQuestionBank role
        return! loadExisting questionId existing
    }
    |> present QuestionRules.toSnapshot

/// Lista paginada e filtrada do banco de questões (somente Admin).
///
/// Execução em dois estágios: primeiro autoriza e valida a consulta (sem precisar de dados);
/// só então devolve a função que, recebendo o banco carregado pelo mundo externo, produz a página.
/// Assim nenhum I/O é feito para requisições que já seriam recusadas.
[<CompiledName("ListQuestions")>]
let listQuestions (role: string) (query: QuestionQuery) : Result<WithBank<QuestionPage>, ErrorView> =
    result {
        let! _ = Authorization.authorize Permission.ViewQuestionBank role
        let! filter, page = QuestionCatalog.validateQuery query |> Result.mapError InvalidInput

        return
            QuestionRules.fromSnapshots
            >> Result.map (QuestionCatalog.search filter page)
            >> present Views.questionPage
    }
    |> Result.mapError Views.error

/// Gera uma prova aleatória a partir do banco (em dois estágios, como listQuestions).
/// O gabarito só é incluído para quem tiver a permissão ViewAnswerKey.
[<CompiledName("GenerateExam")>]
let generateExam
    (role: string)
    (seed: int64)
    (now: DateTimeOffset)
    (input: ExamInput)
    : Result<WithBank<ExamView>, ErrorView> =
    result {
        let! actor = Authorization.authorize Permission.GenerateExam role
        let! blueprint = ExamGeneration.validate input |> Result.mapError InvalidInput
        let! _ = Authorization.requireWhen blueprint.IncludeAnswerKey Permission.ViewAnswerKey actor

        return
            QuestionRules.fromSnapshots
            >> Result.bind (ExamGeneration.generate seed now blueprint)
            >> present Views.exam
    }
    |> Result.mapError Views.error

/// Corrige automaticamente as respostas submetidas (em dois estágios). O mundo externo
/// precisa carregar apenas as questões referenciadas na submissão.
[<CompiledName("GradeExam")>]
let gradeExam (role: string) (input: SubmissionInput) : Result<WithBank<GradeReportView>, ErrorView> =
    result {
        let! _ = Authorization.authorize Permission.GradeExam role

        return
            QuestionRules.fromSnapshots
            >> Result.bind (fun questions -> Grading.gradeSubmission questions input)
            >> present Views.gradeReport
    }
    |> Result.mapError Views.error

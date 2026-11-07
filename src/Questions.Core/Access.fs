namespace Questions.Core

open System

/// Papéis de usuário reconhecidos pelo domínio.
[<RequireQualifiedAccess>]
type Role =
    | Admin
    | Student

/// Permissões de negócio. As regras de quem pode o quê vivem aqui, no núcleo funcional,
/// e não espalhadas em atributos da camada web.
[<RequireQualifiedAccess>]
type Permission =
    /// Cadastrar, alterar e excluir questões.
    | ManageQuestions
    /// Consultar o banco de questões (inclui as respostas corretas).
    | ViewQuestionBank
    /// Gerar provas aleatórias.
    | GenerateExam
    /// Ver o gabarito de uma prova gerada.
    | ViewAnswerKey
    /// Submeter respostas para correção automática.
    | GradeExam

[<RequireQualifiedAccess>]
module Permission =

    let code =
        function
        | Permission.ManageQuestions -> "ManageQuestions"
        | Permission.ViewQuestionBank -> "ViewQuestionBank"
        | Permission.GenerateExam -> "GenerateExam"
        | Permission.ViewAnswerKey -> "ViewAnswerKey"
        | Permission.GradeExam -> "GradeExam"

    let describe =
        function
        | Permission.ManageQuestions -> "gerenciar questões"
        | Permission.ViewQuestionBank -> "consultar o banco de questões"
        | Permission.GenerateExam -> "gerar provas"
        | Permission.ViewAnswerKey -> "visualizar o gabarito"
        | Permission.GradeExam -> "corrigir provas"

[<RequireQualifiedAccess>]
module Role =

    let all = [ Role.Admin; Role.Student ]

    let code =
        function
        | Role.Admin -> "Admin"
        | Role.Student -> "Student"

    let tryParse (raw: string) : Role option =
        all |> List.tryFind (fun role -> String.Equals(code role, raw, StringComparison.OrdinalIgnoreCase))

    /// Matriz de permissões por papel.
    let permissions =
        function
        | Role.Admin ->
            Set.ofList
                [ Permission.ManageQuestions
                  Permission.ViewQuestionBank
                  Permission.GenerateExam
                  Permission.ViewAnswerKey
                  Permission.GradeExam ]
        | Role.Student -> Set.ofList [ Permission.GenerateExam; Permission.GradeExam ]

    let can (permission: Permission) (role: Role) : bool = permissions role |> Set.contains permission

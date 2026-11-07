/// Autorização como funções puras e componíveis.
module Questions.Core.Authorization

/// Reconhece o papel informado pelo mundo externo (ex.: claim de um token).
let identify (rawRole: string) : Result<Role, AppError> =
    Role.tryParse rawRole |> Result.ofOption (UnknownRole rawRole)

/// Exige uma permissão de um papel já identificado.
let require (permission: Permission) (role: Role) : Result<Role, AppError> =
    role |> Role.can permission |> Result.requireTrue (Forbidden(role, permission)) |> Result.map (fun () -> role)

/// Identifica o papel e exige a permissão — pura composição de funções.
let authorize (permission: Permission) : string -> Result<Role, AppError> =
    identify >> Result.bind (require permission)

/// Exige a permissão apenas quando a condição for verdadeira.
let requireWhen (condition: bool) (permission: Permission) (role: Role) : Result<Role, AppError> =
    if condition then require permission role else Ok role

namespace Questions.Core

/// Utilitários funcionais genéricos: composição de Result, validação aplicativa
/// (acumulando erros) e computation expressions.
[<AutoOpen>]
module Prelude =

    /// Inverte a ordem dos dois primeiros argumentos de uma função.
    let flip f a b = f b a

    /// Constrói um predicado que é verdadeiro quando ambos os predicados o são.
    let (<&&>) (p: 'a -> bool) (q: 'a -> bool) = fun x -> p x && q x

/// Funções complementares ao módulo Result da FSharp.Core.
[<RequireQualifiedAccess>]
module Result =

    /// Converte um Option em Result usando o erro informado para o caso None.
    let ofOption (error: 'e) (option: 'a option) : Result<'a, 'e> =
        match option with
        | Some value -> Ok value
        | None -> Error error

    /// Sucesso quando a condição é verdadeira.
    let requireTrue (error: 'e) (condition: bool) : Result<unit, 'e> =
        if condition then Ok () else Error error

    /// Aplica uma função que pode falhar a cada item, parando no primeiro erro (monádico).
    let traverse (f: 'a -> Result<'b, 'e>) (items: 'a list) : Result<'b list, 'e> =
        let folder item state =
            match f item, state with
            | Ok head, Ok tail -> Ok(head :: tail)
            | Error e, _ -> Error e
            | _, Error e -> Error e

        List.foldBack folder items (Ok [])

/// Resultado de uma validação: em caso de falha carrega TODOS os erros encontrados.
type Validation<'a, 'e> = Result<'a, 'e list>

/// Validação aplicativa: diferente de Result.bind, acumula erros em vez de parar no primeiro.
[<RequireQualifiedAccess>]
module Validation =

    let succeed (value: 'a) : Validation<'a, 'e> = Ok value

    let fail (error: 'e) : Validation<'a, 'e> = Error [ error ]

    let map (f: 'a -> 'b) (v: Validation<'a, 'e>) : Validation<'b, 'e> = Result.map f v

    /// Combina duas validações independentes acumulando os erros de ambas.
    let zip (left: Validation<'a, 'e>) (right: Validation<'b, 'e>) : Validation<'a * 'b, 'e> =
        match left, right with
        | Ok a, Ok b -> Ok(a, b)
        | Error e1, Error e2 -> Error(e1 @ e2)
        | Error e, Ok _
        | Ok _, Error e -> Error e

    /// Aplicação de uma função "embrulhada" em uma validação (o <*> aplicativo).
    let apply (f: Validation<'a -> 'b, 'e>) (v: Validation<'a, 'e>) : Validation<'b, 'e> =
        zip f v |> map (fun (g, a) -> g a)

    /// Valida uma condição.
    let require (error: 'e) (condition: bool) : Validation<unit, 'e> =
        if condition then succeed () else fail error

    /// Valida cada item de uma lista acumulando os erros de todos eles.
    let traverse (f: 'a -> Validation<'b, 'e>) (items: 'a list) : Validation<'b list, 'e> =
        List.foldBack (fun item state -> zip (f item) state |> map List.Cons) items (succeed [])

    /// Como traverse, mas a função recebe também o índice (base zero) do item.
    let traverseIndexed (f: int -> 'a -> Validation<'b, 'e>) (items: 'a list) : Validation<'b list, 'e> =
        items |> List.indexed |> traverse (fun (index, item) -> f index item)

    /// Junta uma lista de validações de unit numa só, acumulando os erros.
    let all (validations: Validation<unit, 'e> list) : Validation<unit, 'e> =
        validations |> traverse id |> map ignore

    /// Converte um Option em validação.
    let ofOption (error: 'e) (option: 'a option) : Validation<'a, 'e> =
        option |> Result.ofOption [ error ]

/// Computation expression para Result (encadeamento monádico, curto-circuito no primeiro erro).
type ResultBuilder() =
    member _.Return(value: 'a) : Result<'a, 'e> = Ok value
    member _.ReturnFrom(result: Result<'a, 'e>) = result
    member _.Bind(result: Result<'a, 'e>, binder: 'a -> Result<'b, 'e>) = Result.bind binder result
    member _.Zero() : Result<unit, 'e> = Ok()

/// Computation expression de validação: `let! ... and! ...` combina as validações
/// de forma aplicativa, acumulando todos os erros.
type ValidationBuilder() =
    member _.Return(value: 'a) : Validation<'a, 'e> = Ok value
    member _.ReturnFrom(validation: Validation<'a, 'e>) = validation
    member _.BindReturn(validation: Validation<'a, 'e>, f: 'a -> 'b) = Validation.map f validation
    member _.MergeSources(left: Validation<'a, 'e>, right: Validation<'b, 'e>) = Validation.zip left right
    member _.Bind(validation: Validation<'a, 'e>, binder: 'a -> Validation<'b, 'e>) = Result.bind binder validation
    member _.Zero() : Validation<unit, 'e> = Ok()

[<AutoOpen>]
module Builders =
    /// `result { ... }`
    let result = ResultBuilder()

    /// `validation { let! a = ... and! b = ... }`
    let validation = ValidationBuilder()

/// Consulta do banco de questões: filtros, ordenação e paginação puros.
module Questions.Core.QuestionCatalog

open System
open Questions.Core.Contracts

let maxPageSize = 100

type QuestionFilter =
    { Type: QuestionType option
      Difficulty: Difficulty option
      Search: string option }

type PageRequest = { Page: int; PageSize: int }

type Page<'a> =
    { Items: 'a list
      Page: int
      PageSize: int
      TotalItems: int
      TotalPages: int }

let private validatePage (query: QuestionQuery) : Validation<PageRequest, ValidationError> =
    validation {
        let! () = Validation.require (InvalidPage query.Page) (query.Page >= 1)

        and! () =
            Validation.require
                (InvalidPageSize(maxPageSize, query.PageSize))
                (query.PageSize >= 1 && query.PageSize <= maxPageSize)

        return
            { Page = query.Page
              PageSize = query.PageSize }
    }

/// Valida a consulta, acumulando todos os erros.
let validateQuery (query: QuestionQuery) : Validation<QuestionFilter * PageRequest, ValidationError> =
    validation {
        let! questionType = QuestionType.parseOptional query.Type
        and! difficulty = Difficulty.parseOptional query.Difficulty
        and! page = validatePage query

        let search =
            query.Search |> Option.ofObj |> Option.map _.Trim() |> Option.filter (String.IsNullOrEmpty >> not)

        return
            { Type = questionType
              Difficulty = difficulty
              Search = search },
            page
    }

let private matchesOptional (selector: Question -> 'a) (expected: 'a option) (question: Question) =
    expected |> Option.forall ((=) (selector question))

let private matchesSearch (search: string option) (question: Question) =
    search
    |> Option.forall (fun term ->
        (Question.statement question).Contains(term, StringComparison.OrdinalIgnoreCase)
        || question
           |> Question.options
           |> List.exists (fun option -> (OptionText.value option.Text).Contains(term, StringComparison.OrdinalIgnoreCase)))

/// Predicado composto a partir de predicados menores.
let matches (filter: QuestionFilter) : Question -> bool =
    matchesOptional Question.questionType filter.Type
    <&&> matchesOptional Question.difficulty filter.Difficulty
    <&&> matchesSearch filter.Search

/// Ordenação estável: mais recentes primeiro, desempate pelo identificador.
let sort: Question list -> Question list =
    List.sortWith (fun a b ->
        match compare b.CreatedAt a.CreatedAt with
        | 0 -> compare (QuestionId.value a.Id) (QuestionId.value b.Id)
        | other -> other)

let paginate (request: PageRequest) (items: 'a list) : Page<'a> =
    let total = List.length items

    { Items = items |> List.skip (min total ((request.Page - 1) * request.PageSize)) |> List.truncate request.PageSize
      Page = request.Page
      PageSize = request.PageSize
      TotalItems = total
      TotalPages = (total + request.PageSize - 1) / request.PageSize }

/// Pipeline completo de consulta.
let search (filter: QuestionFilter) (page: PageRequest) : Question list -> Page<Question> =
    List.filter (matches filter) >> sort >> paginate page

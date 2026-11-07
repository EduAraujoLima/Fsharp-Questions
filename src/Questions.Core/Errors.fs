namespace Questions.Core

open System

/// Todos os erros de validação possíveis, como dados (e não exceções).
type ValidationError =
    // --- Questões ---
    | QuestionTypeRequired
    | UnknownQuestionType of value: string
    | UnknownDifficulty of value: string
    | StatementRequired
    | StatementTooLong of maxLength: int
    | OptionTextRequired of position: int
    | OptionTextTooLong of position: int * maxLength: int
    | TooFewOptions of minimum: int * actual: int
    | TooManyOptions of maximum: int * actual: int
    | DuplicateOption of text: string
    | ExactlyOneCorrectOptionRequired of actual: int
    | AtLeastOneCorrectOptionRequired
    | OptionsNotAllowed of questionType: string
    // --- Consulta paginada ---
    | InvalidPage of value: int
    | InvalidPageSize of maximum: int * actual: int
    // --- Geração de prova ---
    | ExamTitleRequired
    | ExamTitleTooLong of maxLength: int
    | ExamSectionsRequired
    | TooManyExamSections of maximum: int * actual: int
    | SectionCountOutOfRange of minimum: int * maximum: int * actual: int
    | TotalQuestionsExceeded of maximum: int * requested: int
    // --- Correção ---
    | AnswersRequired
    | DuplicateAnswer of questionId: Guid
    | UnknownQuestion of questionId: Guid
    | UnknownOption of questionId: Guid * optionId: int
    | SingleOptionExpected of questionId: Guid * selected: int
    // --- Contexto (erros aninhados em coleções) ---
    | InSection of index: int * error: ValidationError
    | InAnswer of index: int * error: ValidationError

/// Erros de aplicação: tudo o que pode dar errado em um caso de uso, como um tipo fechado.
type AppError =
    | InvalidInput of errors: ValidationError list
    | QuestionNotFound of questionId: Guid
    | UnknownRole of role: string
    | Forbidden of role: Role * permission: Permission
    | InsufficientQuestions of criteria: string * requested: int * available: int
    | CorruptedQuestion of questionId: Guid * errors: ValidationError list

[<RequireQualifiedAccess>]
module ValidationError =

    let rec code =
        function
        | QuestionTypeRequired -> "question_type_required"
        | UnknownQuestionType _ -> "unknown_question_type"
        | UnknownDifficulty _ -> "unknown_difficulty"
        | StatementRequired -> "statement_required"
        | StatementTooLong _ -> "statement_too_long"
        | OptionTextRequired _ -> "option_text_required"
        | OptionTextTooLong _ -> "option_text_too_long"
        | TooFewOptions _ -> "too_few_options"
        | TooManyOptions _ -> "too_many_options"
        | DuplicateOption _ -> "duplicate_option"
        | ExactlyOneCorrectOptionRequired _ -> "exactly_one_correct_option_required"
        | AtLeastOneCorrectOptionRequired -> "at_least_one_correct_option_required"
        | OptionsNotAllowed _ -> "options_not_allowed"
        | InvalidPage _ -> "invalid_page"
        | InvalidPageSize _ -> "invalid_page_size"
        | ExamTitleRequired -> "exam_title_required"
        | ExamTitleTooLong _ -> "exam_title_too_long"
        | ExamSectionsRequired -> "exam_sections_required"
        | TooManyExamSections _ -> "too_many_exam_sections"
        | SectionCountOutOfRange _ -> "section_count_out_of_range"
        | TotalQuestionsExceeded _ -> "total_questions_exceeded"
        | AnswersRequired -> "answers_required"
        | DuplicateAnswer _ -> "duplicate_answer"
        | UnknownQuestion _ -> "unknown_question"
        | UnknownOption _ -> "unknown_option"
        | SingleOptionExpected _ -> "single_option_expected"
        | InSection(_, inner)
        | InAnswer(_, inner) -> code inner

    /// Caminho do campo (no contrato de entrada) ao qual o erro se refere.
    let rec field =
        function
        | QuestionTypeRequired
        | UnknownQuestionType _ -> "type"
        | UnknownDifficulty _ -> "difficulty"
        | StatementRequired
        | StatementTooLong _ -> "statement"
        | OptionTextRequired position
        | OptionTextTooLong(position, _) -> $"options[{position - 1}].text"
        | TooFewOptions _
        | TooManyOptions _
        | DuplicateOption _
        | ExactlyOneCorrectOptionRequired _
        | AtLeastOneCorrectOptionRequired
        | OptionsNotAllowed _ -> "options"
        | InvalidPage _ -> "page"
        | InvalidPageSize _ -> "pageSize"
        | ExamTitleRequired
        | ExamTitleTooLong _ -> "title"
        | ExamSectionsRequired
        | TooManyExamSections _
        | TotalQuestionsExceeded _ -> "sections"
        | SectionCountOutOfRange _ -> "count"
        | AnswersRequired
        | DuplicateAnswer _
        | UnknownQuestion _ -> "answers"
        | UnknownOption _
        | SingleOptionExpected _ -> "selectedOptionIds"
        | InSection(index, inner) -> $"sections[{index}].{field inner}"
        | InAnswer(index, inner) ->
            match inner with
            | AnswersRequired
            | DuplicateAnswer _
            | UnknownQuestion _ -> $"answers[{index}].questionId"
            | other -> $"answers[{index}].{field other}"

    /// Mensagem legível (pt-BR).
    let rec message =
        function
        | QuestionTypeRequired -> "O tipo da questão é obrigatório."
        | UnknownQuestionType value -> $"Tipo de questão desconhecido: '{value}'."
        | UnknownDifficulty value -> $"Dificuldade desconhecida: '{value}'."
        | StatementRequired -> "O enunciado é obrigatório."
        | StatementTooLong maxLength -> $"O enunciado deve ter no máximo {maxLength} caracteres."
        | OptionTextRequired position -> $"O texto da alternativa {position} é obrigatório."
        | OptionTextTooLong(position, maxLength) ->
            $"O texto da alternativa {position} deve ter no máximo {maxLength} caracteres."
        | TooFewOptions(minimum, actual) -> $"Informe pelo menos {minimum} alternativas (recebidas: {actual})."
        | TooManyOptions(maximum, actual) -> $"Informe no máximo {maximum} alternativas (recebidas: {actual})."
        | DuplicateOption text -> $"A alternativa '{text}' está repetida."
        | ExactlyOneCorrectOptionRequired actual ->
            $"Exatamente uma alternativa deve ser a correta (marcadas: {actual})."
        | AtLeastOneCorrectOptionRequired -> "Pelo menos uma alternativa deve ser marcada como correta."
        | OptionsNotAllowed questionType -> $"Questões do tipo {questionType} não possuem alternativas."
        | InvalidPage value -> $"A página deve ser maior ou igual a 1 (recebida: {value})."
        | InvalidPageSize(maximum, actual) -> $"O tamanho da página deve estar entre 1 e {maximum} (recebido: {actual})."
        | ExamTitleRequired -> "O título da prova é obrigatório."
        | ExamTitleTooLong maxLength -> $"O título da prova deve ter no máximo {maxLength} caracteres."
        | ExamSectionsRequired -> "Informe ao menos uma seção com a composição da prova."
        | TooManyExamSections(maximum, actual) -> $"A prova pode ter no máximo {maximum} seções (recebidas: {actual})."
        | SectionCountOutOfRange(minimum, maximum, actual) ->
            $"A quantidade de questões da seção deve estar entre {minimum} e {maximum} (recebida: {actual})."
        | TotalQuestionsExceeded(maximum, requested) ->
            $"A prova pode ter no máximo {maximum} questões (solicitadas: {requested})."
        | AnswersRequired -> "Informe ao menos uma resposta."
        | DuplicateAnswer questionId -> $"A questão {questionId} foi respondida mais de uma vez."
        | UnknownQuestion questionId -> $"A questão {questionId} não existe no banco de questões."
        | UnknownOption(questionId, optionId) -> $"A alternativa {optionId} não existe na questão {questionId}."
        | SingleOptionExpected(questionId, selected) ->
            $"A questão {questionId} aceita apenas uma alternativa (selecionadas: {selected})."
        | InSection(index, inner) -> $"Seção {index + 1}: {message inner}"
        | InAnswer(index, inner) -> $"Resposta {index + 1}: {message inner}"

[<RequireQualifiedAccess>]
module AppError =

    let code =
        function
        | InvalidInput _ -> "invalid_input"
        | QuestionNotFound _ -> "question_not_found"
        | UnknownRole _ -> "unknown_role"
        | Forbidden _ -> "forbidden"
        | InsufficientQuestions _ -> "insufficient_questions"
        | CorruptedQuestion _ -> "corrupted_question"

    let message =
        function
        | InvalidInput errors -> $"A requisição possui {List.length errors} erro(s) de validação."
        | QuestionNotFound questionId -> $"Questão {questionId} não encontrada."
        | UnknownRole role -> $"Papel de usuário desconhecido: '{role}'."
        | Forbidden(role, permission) ->
            $"O papel {Role.code role} não tem permissão para {Permission.describe permission}."
        | InsufficientQuestions(criteria, requested, available) ->
            $"Não há questões suficientes no banco para '{criteria}': solicitadas {requested}, disponíveis {available}."
        | CorruptedQuestion(questionId, _) -> $"A questão {questionId} armazenada está em um estado inválido."

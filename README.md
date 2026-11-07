# Fsharp-Questions

Banco de questões e gerador de provas aleatórias em **.NET 10**.

- **F#**: toda a modelagem e as regras de negócio, escritas de forma **puramente funcional**: sem mutabilidade, sem exceções, com tipos algébricos e composição de funções.
- **C#**: a parte que conversa com o mundo (HTTP, autenticação JWT, banco SQLite, relógio, geração de IDs e de entropia), exposta como **API REST**.

Para separar as duas partes, o projeto segue o padrão **functional core, imperative shell**. O shell em C# junta os dados de que o caso de uso precisa e entrega tudo ao núcleo F#. O núcleo devolve uma *decisão* (`Result`). Com ela, o shell grava no banco ou monta a resposta HTTP.

```
┌──────────────────────────── C# (mundo / efeitos) ────────────────────────────┐
│  Questions.Api            Minimal APIs, JWT, OpenAPI/Scalar, Problem Details │
│  Questions.Infrastructure EF Core + SQLite, repositório, interop F#↔C#       │
└───────────────┬──────────────────────────────────────────────▲───────────────┘
                │ contratos imutáveis (QuestionInput, ExamInput…)  │ Result<View, ErrorView>
┌───────────────▼──────────────── F# (núcleo puro) ──────────────┴──────────────┐
│  Questions.Core  Domínio, validação aplicativa, autorização, geração de prova │
│                  com PRNG puro, correção automática, casos de uso             │
└───────────────────────────────────────────────────────────────────────────────┘
```

## Estrutura da solution

| Projeto | Linguagem | Responsabilidade |
|---|---|---|
| `src/Questions.Core` | F# | Domínio e regras de negócio puras |
| `src/Questions.Infrastructure` | C# | Persistência (EF Core/SQLite) e adaptadores de interop |
| `src/Questions.Api` | C# | API REST, autenticação, OpenAPI, seed de dados |
| `tests/Questions.Core.Tests` | F# | Testes unitários e de propriedade (xUnit + FsCheck) |
| `tests/Questions.Api.Tests` | C# | Testes de integração (WebApplicationFactory) |

### O núcleo F#, arquivo por arquivo

Em F# a ordem de compilação importa, e cada arquivo só enxerga os anteriores:

| Arquivo | Conteúdo |
|---|---|
| `Prelude.fs` | `Validation<'a,'e>` aplicativa (acumula erros), `traverse`, CEs `result { }` e `validation { let! … and! … }` |
| `Rng.fs` | Gerador pseudoaleatório **puro** (SplitMix64) como mônada State (`Rng<'a> = Seed -> 'a * Seed`), CE `rng { }`, `shuffle` sem mutação |
| `Access.fs` | `Role`, `Permission` e a matriz de permissões por papel |
| `Errors.fs` | Todos os erros como **dados** (`ValidationError`, `AppError`), com código, campo e mensagem |
| `Domain.fs` | Tipos do domínio. *Constrained types* com construtor privado (`Statement`, `OptionText`, `Alternatives`). Questão dissertativa simplesmente não tem alternativas (estado inválido irrepresentável) |
| `Contracts.fs` | Registros imutáveis com tipos primitivos que formam a fronteira com o C# |
| `QuestionRules.fs` | Cadastro, revisão (nova versão imutável) e reidratação revalidada dos dados persistidos |
| `Authorization.fs` | `authorize = identify >> Result.bind (require permission)` |
| `QuestionCatalog.fs` | Filtros como predicados compostos (`<&&>`), ordenação e paginação |
| `ExamGeneration.fs` | Validação do pedido e sorteio determinístico das questões |
| `Grading.fs` | Correção automática |
| `Views.fs` | Conversão do domínio para os contratos de saída |
| `Workflows.fs` | Casos de uso consumidos pelo C# (com `[<CompiledName>]` em PascalCase) |

Algumas decisões de projeto:

- **Nenhum efeito colateral no núcleo.** Data/hora, IDs (`Guid.CreateVersion7`) e a semente aleatória (`RandomNumberGenerator`) vêm do C#. Por isso a mesma entrada sempre produz a mesma saída, e os testes não precisam de mocks.
- **Casos de uso em dois estágios.** A listagem, a geração e a correção de provas primeiro autorizam e validam a requisição, ainda sem dados. Só então devolvem uma função `WithBank<'a> = QuestionSnapshot seq -> Result<'a, ErrorView>`, que o C# chama depois de carregar o banco. Assim, uma requisição que seria negada não faz nenhum I/O.
- **Erros acumulados.** Na validação aplicativa, uma requisição inválida recebe *todos* os problemas de uma vez, e cada um traz o caminho do campo (`options[2].text`, `sections[0].count`…).
- **Dados persistidos não são confiáveis.** Ao serem lidos, os snapshots do banco passam de novo pelas regras do domínio. Um registro corrompido vira o erro `CorruptedQuestion`, sem lançar exceção.

## Regras de negócio

### Tipos de questão (`GET /api/question-types`)

| Tipo | Alternativas | Corretas |
|---|---|---|
| `SingleChoice` | 2 a 10 | exatamente 1 |
| `MultipleChoice` | 2 a 10 | ao menos 1 |
| `TrueFalse` | exatamente 2 | exatamente 1 |
| `Essay` | nenhuma | correção manual |

Outras regras do cadastro:

- O enunciado é obrigatório (até 4000 caracteres).
- Cada alternativa precisa de texto (até 1000 caracteres), e não pode haver duas alternativas com o mesmo texto (sem diferenciar maiúsculas de minúsculas).
- A dificuldade pode ser `Easy`, `Medium` ou `Hard`. Se for omitida, vale `Medium`.
- Cada alteração gera uma nova versão. O EF Core usa essa versão para controle otimista de concorrência: duas alterações simultâneas resultam em **409**.

### Permissões (definidas no F#)

| Ação | Admin | Student |
|---|:-:|:-:|
| Cadastrar, alterar e excluir questões | ✅ | ❌ |
| Listar e consultar o banco (com gabarito) | ✅ | ❌ |
| Gerar prova | ✅ | ✅ |
| Gerar prova **com gabarito** | ✅ | ❌ |
| Corrigir respostas | ✅ | ✅ |

### Geração de prova

O pedido é dividido em seções. Cada seção pede `count` questões e pode filtrar por `type` e/ou `difficulty` (os dois são opcionais).

- **Sem repetição:** cada seção sorteia apenas entre as questões que ainda não foram escolhidas.
- **Seções mais específicas primeiro:** assim, uma seção do tipo "qualquer tipo" não consome as questões de que uma seção específica precisa.
- **Ordem aleatória:** a ordem final das questões é sorteada, e as alternativas também são embaralhadas (`shuffleOptions`, padrão `true`). Os ids originais das alternativas são mantidos e os rótulos A, B, C… são reatribuídos.
- **Reprodutível:** a resposta traz a `seed` usada. Enviar a mesma `seed` com o mesmo banco gera exatamente a mesma prova.
- **Banco insuficiente:** se faltarem questões para atender uma seção, a API responde **422** dizendo quantas foram pedidas e quantas existem.
- **Limites:** até 20 seções e até 100 questões por prova.

### Correção

- `SingleChoice`, `TrueFalse` e `MultipleChoice` só contam como corretas se as alternativas marcadas forem exatamente as do gabarito.
- `Essay` respondida fica `PendingReview` (correção manual).
- A nota (`scorePercent`) considera apenas as questões objetivas.

## Como executar

Pré-requisito: [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/Questions.Api   # http://localhost:5203
```

Em ambiente de desenvolvimento:

- O banco `questions.db` (SQLite) é criado automaticamente e populado com 12 questões de exemplo.
- A documentação interativa (Scalar) fica em `http://localhost:5203/scalar/v1`, e o documento OpenAPI em `/openapi/v1.json`.
- O arquivo `src/Questions.Api/Questions.Api.http` traz requisições prontas para todos os endpoints.

Usuários de demonstração, configurados em `appsettings.Development.json`:

| Usuário | Senha | Papel |
|---|---|---|
| `admin` | `admin123` | Admin |
| `aluno` | `aluno123` | Student |

> ⚠️ Os usuários e a chave de assinatura servem apenas para demonstração. Em produção, defina `Auth__SigningKey` (32 caracteres ou mais) por variável de ambiente ou secret store, e troque os usuários em configuração por um provedor de identidade.

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/auth/token` | Login. Devolve um JWT (`Bearer`) |
| `GET` | `/api/question-types` | Tipos de questão e suas regras (público) |
| `GET` | `/api/questions?type=&difficulty=&search=&page=1&pageSize=20` | Lista paginada (Admin) |
| `GET` | `/api/questions/{id}` | Detalhe com gabarito (Admin) |
| `POST` | `/api/questions` | Cadastro (Admin) → `201` |
| `PUT` | `/api/questions/{id}` | Alteração, gerando nova versão (Admin) |
| `DELETE` | `/api/questions/{id}` | Exclusão (Admin) → `204` |
| `POST` | `/api/exams` | Gera uma prova aleatória |
| `POST` | `/api/exams/grade` | Corrige as respostas |

Os erros seguem o formato *Problem Details* (RFC 9457) e sempre trazem um campo `code`:

| Status | Quando |
|---|---|
| `400` | Validação. A resposta lista os erros por campo em `errors` e `details` |
| `401` | Não autenticado |
| `403` | Papel sem permissão |
| `404` | Questão não encontrada |
| `409` | Conflito de versão |
| `422` | Regra de negócio violada (por exemplo, banco insuficiente) |

Exemplo de cadastro:

```json
POST /api/questions
{
  "type": "SingleChoice",
  "statement": "Qual palavra-chave declara um valor imutável em F#?",
  "difficulty": "Easy",
  "options": [
    { "text": "let", "isCorrect": true },
    { "text": "var", "isCorrect": false },
    { "text": "mutable", "isCorrect": false }
  ]
}
```

Exemplo de geração de prova:

```json
POST /api/exams
{
  "title": "Prova 1",
  "sections": [
    { "type": "SingleChoice", "count": 3 },
    { "type": "TrueFalse", "difficulty": "Hard", "count": 1 },
    { "count": 2 }
  ],
  "shuffleOptions": true,
  "includeAnswerKey": false,
  "seed": 42
}
```

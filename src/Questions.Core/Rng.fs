namespace Questions.Core

/// Semente imutável de um gerador pseudoaleatório puro (algoritmo SplitMix64).
/// A aleatoriedade "de verdade" (entropia) vem de fora: o mundo (C#) fornece a semente
/// e o núcleo funcional apenas a transforma, de forma determinística e reprodutível.
type Seed = private Seed of uint64

/// Uma computação aleatória pura: recebe uma semente e devolve um valor e a próxima semente.
/// É a mônada State especializada para Seed.
type Rng<'a> = Seed -> 'a * Seed

[<RequireQualifiedAccess>]
module Seed =

    let ofInt64 (value: int64) : Seed = Seed(uint64 value)

[<RequireQualifiedAccess>]
module Rng =

    /// Executa a computação devolvendo o valor e a semente final.
    let run (seed: Seed) (computation: Rng<'a>) : 'a * Seed = computation seed

    /// Executa a computação devolvendo apenas o valor.
    let eval (seed: Seed) (computation: Rng<'a>) : 'a = computation seed |> fst

    /// Computação que não consome aleatoriedade.
    let ret (value: 'a) : Rng<'a> = fun seed -> value, seed

    let bind (binder: 'a -> Rng<'b>) (computation: Rng<'a>) : Rng<'b> =
        fun seed ->
            let value, next = computation seed
            binder value next

    let map (f: 'a -> 'b) (computation: Rng<'a>) : Rng<'b> = computation |> bind (f >> ret)

    /// Próximo inteiro de 64 bits (SplitMix64). Aritmética sem checagem de overflow,
    /// como o algoritmo exige.
    let nextUInt64: Rng<uint64> =
        fun (Seed state) ->
            let next = state + 0x9E3779B97F4A7C15UL
            let z1 = (next ^^^ (next >>> 30)) * 0xBF58476D1CE4E5B9UL
            let z2 = (z1 ^^^ (z1 >>> 27)) * 0x94D049BB133111EBUL
            z2 ^^^ (z2 >>> 31), Seed next

    /// Inteiro em [0, bound).
    let nextInt (bound: int) : Rng<int> =
        nextUInt64 |> map (fun value -> int (value % uint64 (max bound 1)))

    /// Aplica uma computação aleatória a cada item, encadeando a semente da esquerda para a direita.
    let traverse (f: 'a -> Rng<'b>) (items: 'a list) : Rng<'b list> =
        List.foldBack
            (fun item rest -> f item |> bind (fun head -> rest |> map (fun tail -> head :: tail)))
            items
            (ret [])

    /// Embaralha uma lista sem mutação: associa uma chave aleatória a cada item e ordena por ela.
    /// A ordenação é estável, então o resultado depende apenas da semente e da lista de entrada.
    let shuffle (items: 'a list) : Rng<'a list> =
        items
        |> traverse (fun item -> nextUInt64 |> map (fun key -> key, item))
        |> map (List.sortBy fst >> List.map snd)

/// `rng { let! x = Rng.nextInt 10 ... }`
type RngBuilder() =
    member _.Return(value: 'a) : Rng<'a> = Rng.ret value
    member _.ReturnFrom(computation: Rng<'a>) = computation
    member _.Bind(computation: Rng<'a>, binder: 'a -> Rng<'b>) = Rng.bind binder computation
    member _.Zero() : Rng<unit> = Rng.ret ()

[<AutoOpen>]
module RngBuilderInstance =
    let rng = RngBuilder()

module Questions.Core.Tests.RngTests

open FsCheck.Xunit
open Questions.Core

[<Property>]
let ``shuffle produz uma permutação da lista`` (seed: int64) (items: int list) =
    let shuffled = Rng.shuffle items |> Rng.eval (Seed.ofInt64 seed)
    List.sort shuffled = List.sort items

[<Property>]
let ``shuffle é determinístico para a mesma semente`` (seed: int64) (items: int list) =
    let run () = Rng.shuffle items |> Rng.eval (Seed.ofInt64 seed)
    run () = run ()

[<Property>]
let ``nextInt fica no intervalo`` (seed: int64) (bound: FsCheck.PositiveInt) =
    let value = Rng.nextInt bound.Get |> Rng.eval (Seed.ofInt64 seed)
    value >= 0 && value < bound.Get

[<Property>]
let ``a semente avança a cada sorteio`` (seed: int64) =
    let values = List.replicate 5 Rng.nextUInt64 |> Rng.traverse id |> Rng.eval (Seed.ofInt64 seed)
    List.distinct values = values

[<Property>]
let ``computações compostas com rng { } equivalem a bind explícito`` (seed: int64) =
    let viaBuilder =
        rng {
            let! a = Rng.nextInt 100
            let! b = Rng.nextInt 100
            return a + b
        }

    let viaBind = Rng.nextInt 100 |> Rng.bind (fun a -> Rng.nextInt 100 |> Rng.map ((+) a))
    Rng.eval (Seed.ofInt64 seed) viaBuilder = Rng.eval (Seed.ofInt64 seed) viaBind

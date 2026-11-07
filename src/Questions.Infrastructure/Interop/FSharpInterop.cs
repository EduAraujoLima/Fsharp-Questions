using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace Questions.Infrastructure.Interop;

/// <summary>
/// Pequenos adaptadores para consumir os tipos funcionais do F# (Result, Option, List)
/// de forma idiomática a partir do C#.
/// </summary>
public static class FSharpInterop
{
    /// <summary>Pattern matching sobre um <c>Result</c> do F#.</summary>
    public static TOut Match<T, TError, TOut>(
        this FSharpResult<T, TError> result,
        Func<T, TOut> ok,
        Func<TError, TOut> error) =>
        result.IsOk ? ok(result.ResultValue) : error(result.ErrorValue);

    /// <summary>Pattern matching assíncrono sobre um <c>Result</c> do F#.</summary>
    public static Task<TOut> MatchAsync<T, TError, TOut>(
        this FSharpResult<T, TError> result,
        Func<T, Task<TOut>> ok,
        Func<TError, TOut> error) =>
        result.IsOk ? ok(result.ResultValue) : Task.FromResult(error(result.ErrorValue));

    /// <summary>Converte uma referência possivelmente nula em <c>option</c> do F#.</summary>
    public static FSharpOption<T> ToFSharpOption<T>(this T? value) where T : class =>
        value is null ? FSharpOption<T>.None : FSharpOption<T>.Some(value);

    /// <summary>Converte uma sequência (possivelmente nula) em lista imutável do F#.</summary>
    public static FSharpList<T> ToFSharpList<T>(this IEnumerable<T>? source) =>
        source is null ? FSharpList<T>.Empty : ListModule.OfSeq(source);
}

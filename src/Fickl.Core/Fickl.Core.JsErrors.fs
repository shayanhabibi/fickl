namespace Fickl.Core.JS

#nowarn "1182"

open Fable.Core

// Bindings for the errors JavaScript throws, to catch them by type with `:?`, plus `Thrown` for a thrown value
// that is not an error. They share the `Fickl.Core.JS` namespace with the other JS bindings.

/// <summary>The JavaScript <c>Error</c>, the base of the errors JavaScript throws.</summary>
[<Global; AllowNullLiteral>]
type Error(message: string) =
    inherit System.Exception(message)
    new() = Error("")
    /// <summary>The error's message.</summary>
    member _.message: string = nativeOnly
    /// <summary>The error's name, such as <c>"TypeError"</c> or, for a <c>DOMException</c>, <c>"AbortError"</c>.</summary>
    member _.name: string = nativeOnly
    /// <summary>The stack trace, where the engine provides one.</summary>
    member _.stack: string = nativeOnly
    /// <summary>The value the error was created with as its cause, if any.</summary>
    member _.cause: obj = nativeOnly

/// <summary>The JavaScript <c>TypeError</c>: a value is not of the expected type.</summary>
[<Global; AllowNullLiteral>]
type TypeError(message: string) =
    inherit Error(message)
    new() = TypeError("")

/// <summary>The JavaScript <c>RangeError</c>: a value is outside its allowed range.</summary>
[<Global; AllowNullLiteral>]
type RangeError(message: string) =
    inherit Error(message)
    new() = RangeError("")

/// <summary>The JavaScript <c>SyntaxError</c>, thrown for example by <c>JSON.parse</c>.</summary>
[<Global; AllowNullLiteral>]
type SyntaxError(message: string) =
    inherit Error(message)
    new() = SyntaxError("")

/// <summary>The JavaScript <c>ReferenceError</c>: an undeclared variable was read.</summary>
[<Global; AllowNullLiteral>]
type ReferenceError(message: string) =
    inherit Error(message)
    new() = ReferenceError("")

/// <summary>The JavaScript <c>AggregateError</c>, which holds several errors, thrown for example by <c>Promise.any</c>.</summary>
[<Global; AllowNullLiteral>]
type AggregateError(errors: obj[], message: string) =
    inherit Error(message)
    new(errors: obj[]) = AggregateError(errors, "")
    /// <summary>The errors this error holds.</summary>
    member _.errors: obj[] = nativeOnly

/// <summary>The web <c>DOMException</c>, told apart by its <c>name</c>, such as <c>"AbortError"</c> or <c>"NotFoundError"</c>.</summary>
/// <remarks>A global in browsers, Node.js 17 and later, Deno and Bun.</remarks>
[<Global; AllowNullLiteral>]
type DOMException(message: string, name: string) =
    inherit Error(message)
    new(message: string) = DOMException(message, "Error")
    new() = DOMException("", "Error")
    /// <summary>The legacy numeric code of the error, <c>0</c> for names that have none.</summary>
    member _.code: int = nativeOnly

/// <summary>The <c>Thrown</c> pattern, opened with <c>Fickl.Core.JS</c>.</summary>
/// <example><code lang="fsharp">
/// try f () with
/// | :? JS.TypeError as e -> e.message
/// | :? JS.DOMException as e when e.name = "AbortError" -> "aborted"
/// | JS.Thrown value -> string value
/// | e -> e.Message
/// </code></example>
[<AutoOpen>]
module ErrorPatterns =
    /// <summary>Matches a thrown value that is not an error: neither a JS <c>Error</c> nor an F# exception, such as a
    /// string, a plain object or <c>undefined</c>. The value is passed as it was thrown.</summary>
    /// <remarks>On .NET every thrown value is an exception, so this never matches. An F# <c>async</c> turns a thrown
    /// value that is not an error into an exception, so it does not match there either.</remarks>
    let (|Thrown|_|) (e: exn) : obj option =
        let value = box e

        if value :? exn then
            None
        else
            Some value

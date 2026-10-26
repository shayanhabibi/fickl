namespace Fickl.Core

open System
open Fable.Core

/// <summary>Builds native JavaScript async computations with Promise bindings.</summary>
/// <remarks>Use through <c>jsAsync</c>. Bindings compile to <c>await</c>; execution starts immediately.
/// Supports JavaScript and TypeScript and has no F# Async cancellation token.
/// Return resolved values; use <c>return!</c> to adopt a Promise instead of constructing a nested Promise type.
/// Top-level thenables are rejected at runtime when their types are opaque; use a record to carry a Promise as data.</remarks>
type JsAsyncBuilder() =
    /// <summary>Returns a resolved value from the computation.</summary>
    member _.Return(value: 'T) : JS.Promise<'T> = nativeOnly
    /// <summary>Awaits and returns another Promise.</summary>
    member _.ReturnFrom(promise: JS.Promise<'T>) : JS.Promise<'T> = nativeOnly
    /// <summary>Awaits a Promise before continuing the computation.</summary>
    member _.Bind(promise: JS.Promise<'T>, continuation: 'T -> JS.Promise<'U>) : JS.Promise<'U> = nativeOnly
    /// <summary>Produces a completed unit computation.</summary>
    member _.Zero() : JS.Promise<unit> = nativeOnly
    /// <summary>Defers execution of the computation body.</summary>
    member _.Delay(body: unit -> JS.Promise<'T>) : JS.Promise<'T> = nativeOnly
    /// <summary>Starts a deferred computation as a native async function invocation.</summary>
    member _.Run(body: JS.Promise<'T>) : JS.Promise<'T> = nativeOnly
    /// <summary>Runs two computations in sequence.</summary>
    member _.Combine(first: JS.Promise<unit>, second: JS.Promise<'T>) : JS.Promise<'T> = nativeOnly
    /// <summary>Repeats the body while the guard holds.</summary>
    member _.While(guard: unit -> bool, body: JS.Promise<unit>) : JS.Promise<unit> = nativeOnly
    /// <summary>Runs the body for each value in a sequence.</summary>
    member _.For(values: seq<'T>, body: 'T -> JS.Promise<unit>) : JS.Promise<unit> = nativeOnly
    /// <summary>Awaits each value from an asynchronous source, closing its iterator on early exit.</summary>
    member _.For(values: JS.AsyncIterable<'T>, body: 'T -> JS.Promise<unit>) : JS.Promise<unit> = nativeOnly
    /// <summary>Handles exceptions from a computation, including rejected Promises.</summary>
    member _.TryWith(body: JS.Promise<'T>, handler: exn -> JS.Promise<'T>) : JS.Promise<'T> = nativeOnly
    /// <summary>Runs compensation after the computation settles.</summary>
    member _.TryFinally(body: JS.Promise<'T>, compensation: unit -> unit) : JS.Promise<'T> = nativeOnly

    /// <summary>Disposes a resource after its body settles.</summary>
    member _.Using<'Resource, 'T when 'Resource :> IDisposable>
        (resource: 'Resource, body: 'Resource -> JS.Promise<'T>)
        : JS.Promise<'T>
        =
        nativeOnly

/// <summary>Provides the native JavaScript async computation expression.</summary>
[<AutoOpen; Erase>]
module NativeAsync =
    /// <summary>Runs a computation as a native async function returning a JavaScript Promise.</summary>
    /// <example><code lang="fsharp">
    /// [&lt;AsyncFunction>]
    /// let fetchTwice (promise: JS.Promise&lt;int>) = jsAsync {
    ///     let! first = promise
    ///     let! second = promise
    ///     return first + second
    /// }
    /// </code></example>
    let jsAsync: JsAsyncBuilder = nativeOnly

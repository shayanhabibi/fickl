namespace Fickl.Core

open System
open Fable.Core

/// <summary>Builds native JavaScript async computations with Promise bindings.</summary>
/// <remarks>Use <c>jsAsync</c>; bindings emit <c>await</c> and execution starts immediately. JavaScript/TypeScript only; no F# cancellation token, so pass one to the work: <c>token.ToAbortSignal()</c> for JavaScript APIs, or <c>token.Register</c> in a <c>use</c>.
/// Return resolved values or adopt a Promise with <c>return!</c>. Opaque top-level thenables fail validation; wrap Promise data in a record.
/// Interop payloads must keep their <c>then</c> property stable through validation and native resolution.</remarks>
type JsAsyncBuilder() =
    /// <summary>Returns a resolved value from the computation.</summary>
    member _.Return(value: 'T) : JS.Promise<'T> = nativeOnly
    /// <summary>Awaits and returns another Promise.</summary>
    member _.ReturnFrom(promise: JS.Promise<'T>) : JS.Promise<'T> = nativeOnly
    /// <summary>Awaits a Promise before continuing the computation.</summary>
    member _.Bind(promise: JS.Promise<'T>, continuation: 'T -> JS.Promise<'U>) : JS.Promise<'U> = nativeOnly
    /// <summary>Awaits the Promises of <c>let! ... and! ...</c> together with <c>Promise.all</c>.</summary>
    member _.MergeSources(first: JS.Promise<'T1>, second: JS.Promise<'T2>) : JS.Promise<'T1 * 'T2> = nativeOnly

    /// <summary>Awaits the Promises of <c>let! ... and! ... and! ...</c> together with <c>Promise.all</c>.</summary>
    member _.MergeSources3
        (first: JS.Promise<'T1>, second: JS.Promise<'T2>, third: JS.Promise<'T3>)
        : JS.Promise<'T1 * 'T2 * 'T3>
        =
        nativeOnly

    /// <summary>Awaits four <c>and!</c> Promises together with <c>Promise.all</c>.</summary>
    member _.MergeSources4
        (first: JS.Promise<'T1>, second: JS.Promise<'T2>, third: JS.Promise<'T3>, fourth: JS.Promise<'T4>)
        : JS.Promise<'T1 * 'T2 * 'T3 * 'T4>
        =
        nativeOnly

    /// <summary>Awaits five <c>and!</c> Promises together with <c>Promise.all</c>.</summary>
    member _.MergeSources5
        (
            first: JS.Promise<'T1>,
            second: JS.Promise<'T2>,
            third: JS.Promise<'T3>,
            fourth: JS.Promise<'T4>,
            fifth: JS.Promise<'T5>
        )
        : JS.Promise<'T1 * 'T2 * 'T3 * 'T4 * 'T5>
        =
        nativeOnly

    /// <summary>Awaits six <c>and!</c> Promises together with <c>Promise.all</c>.</summary>
    member _.MergeSources6
        (
            first: JS.Promise<'T1>,
            second: JS.Promise<'T2>,
            third: JS.Promise<'T3>,
            fourth: JS.Promise<'T4>,
            fifth: JS.Promise<'T5>,
            sixth: JS.Promise<'T6>
        )
        : JS.Promise<'T1 * 'T2 * 'T3 * 'T4 * 'T5 * 'T6>
        =
        nativeOnly

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
    /// <summary>Awaits a single Promise and runs the body once with its value, as <c>for value in promise do</c>.</summary>
    member _.For(promise: JS.Promise<'T>, body: 'T -> JS.Promise<'U>) : JS.Promise<'U> = nativeOnly
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

    /// <summary>The <c>jsAsync</c> computation expression under the name Fable.Promise uses, so <c>promise { }</c> compiles to a native async function.</summary>
    let promise: JsAsyncBuilder = nativeOnly

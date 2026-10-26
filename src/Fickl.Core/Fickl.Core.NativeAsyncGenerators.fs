namespace Fickl.Core

open Fable.Core

/// <summary>A typed request to yield and receive a sent value inside an async generator.</summary>
type JsYieldRequest<'Yield, 'Next> = interface end

/// <summary>A native asynchronous resource closed through Symbol.asyncDispose.</summary>
type JsAsyncDisposable =
    /// <summary>Closes the resource and resolves after cleanup finishes.</summary>
    [<Emit("$0[Symbol.asyncDispose]()")>]
    abstract ``Symbol.asyncDispose``: unit -> JS.Promise<unit>

/// <summary>A native JavaScript async generator with typed yielded, terminal, and sent values.</summary>
type JsAsyncGenerator<'Yield, 'Return, 'Next> =
    inherit JS.AsyncIterable<'Yield>
    /// <summary>Resumes execution, optionally supplying the result of the suspended yield expression.</summary>
    abstract next: ?value: 'Next -> JS.Promise<JsGeneratorResult<'Yield, 'Return>>
    /// <summary>Closes the generator with a terminal value, running active finally blocks.</summary>
    abstract ``return``: value: 'Return -> JS.Promise<JsGeneratorResult<'Yield, 'Return>>
    /// <summary>Throws into the suspended yield expression.</summary>
    abstract throw: error: obj -> JS.Promise<JsGeneratorResult<'Yield, 'Return>>

/// <summary>A deferred generator body supplied to the compiler by <c>jsAsyncGenerator</c>.</summary>
type JsAsyncGeneratorCode<'Yield, 'Return, 'Next> = interface end

/// <summary>Builds native JavaScript async generator bodies.</summary>
/// <remarks>Use through <c>jsAsyncGenerator</c>. Execution starts on the first <c>next</c> call.
/// A <c>let!</c> binding awaits a Promise; bind <c>jsYield value</c> to receive a value from the next call.
/// Await promises before yielding or returning; yield and terminal types describe resolved values.</remarks>
type JsAsyncGeneratorBuilder<'Yield, 'Return, 'Next>() =
    /// <summary>Yields a value to the caller.</summary>
    member _.Yield(value: 'Yield) : JsAsyncGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly
    /// <summary>Delegates yielded values to another sequence.</summary>
    member _.YieldFrom(values: seq<'Yield>) : JsAsyncGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Yields a request and continues with the caller's next sent value.</summary>
    member _.Bind
        (value: JsYieldRequest<'Yield, 'Next>, continuation: 'Next -> JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Awaits a Promise before continuing the generator.</summary>
    member _.Bind
        (promise: JS.Promise<'T>, continuation: 'T -> JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Delegates yielded values to an asynchronous source.</summary>
    member _.YieldFrom(values: JS.AsyncIterable<'Yield>) : JsAsyncGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Completes the generator with a terminal value.</summary>
    member _.Return(value: 'Return) : JsAsyncGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Delegates to another generator, including its terminal value and sent values.</summary>
    member _.ReturnFrom
        (generator: JsAsyncGenerator<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Produces an empty generator body.</summary>
    member _.Zero() : JsAsyncGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Defers execution of a generator body.</summary>
    member _.Delay
        (body: unit -> JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Creates a native generator from a deferred body.</summary>
    member _.Run(body: JsAsyncGeneratorCode<'Yield, 'Return, 'Next>) : JsAsyncGenerator<'Yield, 'Return, 'Next> =
        nativeOnly

    /// <summary>Creates a one-way generator with unit input and terminal values.</summary>
    member _.Run(body: JsAsyncGeneratorCode<'Yield, unit, unit>) : JsAsyncGenerator<'Yield, unit, unit> = nativeOnly

    /// <summary>Runs two generator bodies in sequence.</summary>
    member _.Combine
        (first: JsAsyncGeneratorCode<'Yield, 'Return, 'Next>, second: JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Repeats a body while its guard holds.</summary>
    member _.While
        (guard: unit -> bool, body: JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Runs a body for each value in a sequence.</summary>
    member _.For
        (values: seq<'T>, body: 'T -> JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Awaits each value from an asynchronous source before running its body.</summary>
    member _.For
        (values: JS.AsyncIterable<'T>, body: 'T -> JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Handles exceptions, including exceptions sent through <c>throw</c>.</summary>
    member _.TryWith
        (
            body: JsAsyncGeneratorCode<'Yield, 'Return, 'Next>,
            handler: exn -> JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        )
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Runs compensation when the generator leaves its protected body.</summary>
    member _.TryFinally
        (body: JsAsyncGeneratorCode<'Yield, 'Return, 'Next>, compensation: unit -> unit)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Awaits asynchronous disposal, or disposes a synchronous resource, when the generator leaves its body.</summary>
    /// <remarks>The resource must implement IDisposable, IAsyncDisposable, or JsAsyncDisposable.</remarks>
    member _.Using<'Resource>
        (resource: 'Resource, body: 'Resource -> JsAsyncGeneratorCode<'Yield, 'Return, 'Next>)
        : JsAsyncGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

/// <summary>Provides the native JavaScript async generator computation expression.</summary>
[<AutoOpen; Erase>]
module NativeAsyncGenerators =
    /// <summary>Yields a request and receives the value supplied by the following next call.</summary>
    let jsYield<'Yield, 'Next> (value: 'Yield) : JsYieldRequest<'Yield, 'Next> = nativeOnly

    /// <summary>Creates a native generator with typed yield, return, and next values.</summary>
    /// <example><code lang="fsharp">
    /// let conversation () : JsAsyncGenerator&lt;int, string, int> = jsAsyncGenerator {
    ///     let! reply = jsYield 1
    ///     return string reply
    /// }
    /// </code></example>
    let jsAsyncGenerator<'Yield, 'Return, 'Next> : JsAsyncGeneratorBuilder<'Yield, 'Return, 'Next> =
        nativeOnly

namespace Fickl.Core

open System
open Fable.Core

/// <summary>A yielded native iterator result.</summary>
type JsYieldResult<'Yield> =
    /// <summary>The value yielded by the generator.</summary>
    [<Emit("$0.value")>]
    abstract value: 'Yield

/// <summary>A terminal native iterator result.</summary>
type JsReturnResult<'Return> =
    /// <summary>The generator's terminal value.</summary>
    [<Emit("$0.value")>]
    abstract value: 'Return

/// <summary>A native iterator result discriminated by its completion flag.</summary>
[<Erase>]
type JsGeneratorResult<'Yield, 'Return> =
    /// <summary>The generator yielded a value.</summary>
    | [<Fickl.Core.CaseTest("!$0.done")>] Yielded of JsYieldResult<'Yield>
    /// <summary>The generator completed with a terminal value.</summary>
    | [<Fickl.Core.CaseTest("!!$0.done")>] Returned of JsReturnResult<'Return>

    /// <summary>Whether the generator has completed.</summary>
    [<Emit("!!$0.done")>]
    member _.``done``: bool = nativeOnly

/// <summary>A native JavaScript generator with typed yielded, terminal, and sent values.</summary>
type JsGenerator<'Yield, 'Return, 'Next> =
    inherit System.Collections.Generic.IEnumerable<'Yield>
    /// <summary>Resumes execution, optionally supplying the result of the suspended yield expression.</summary>
    abstract next: ?value: 'Next -> JsGeneratorResult<'Yield, 'Return>
    /// <summary>Closes the generator with a terminal value, running active finally blocks.</summary>
    abstract ``return``: value: 'Return -> JsGeneratorResult<'Yield, 'Return>
    /// <summary>Throws into the suspended yield expression.</summary>
    abstract throw: error: obj -> JsGeneratorResult<'Yield, 'Return>

/// <summary>A deferred generator body supplied to the compiler by <c>jsGenerator</c>.</summary>
type JsGeneratorCode<'Yield, 'Return, 'Next> = interface end

/// <summary>Builds native JavaScript generator bodies.</summary>
/// <remarks>Use through <c>jsGenerator</c>. Execution starts on the first <c>next</c> call.
/// A <c>let!</c> binding yields its input and receives a value sent by the following <c>next</c> call.</remarks>
type JsGeneratorBuilder<'Yield, 'Return, 'Next>() =
    /// <summary>Yields a value to the caller.</summary>
    member _.Yield(value: 'Yield) : JsGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly
    /// <summary>Delegates yielded values to another sequence.</summary>
    member _.YieldFrom(values: seq<'Yield>) : JsGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Yields a request and continues with the caller's next sent value.</summary>
    member _.Bind
        (value: 'Yield, continuation: 'Next -> JsGeneratorCode<'Yield, 'Return, 'Next>)
        : JsGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Completes the generator with a terminal value.</summary>
    member _.Return(value: 'Return) : JsGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Delegates to another generator, including its terminal value and sent values.</summary>
    member _.ReturnFrom(generator: JsGenerator<'Yield, 'Return, 'Next>) : JsGeneratorCode<'Yield, 'Return, 'Next> =
        nativeOnly

    /// <summary>Produces an empty generator body.</summary>
    member _.Zero() : JsGeneratorCode<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Defers execution of a generator body.</summary>
    member _.Delay(body: unit -> JsGeneratorCode<'Yield, 'Return, 'Next>) : JsGeneratorCode<'Yield, 'Return, 'Next> =
        nativeOnly

    /// <summary>Creates a native generator from a deferred body.</summary>
    member _.Run(body: JsGeneratorCode<'Yield, 'Return, 'Next>) : JsGenerator<'Yield, 'Return, 'Next> = nativeOnly

    /// <summary>Creates a generator with a unit terminal value when its terminal type is unspecified.</summary>
    member _.Run(body: JsGeneratorCode<'Yield, unit, 'Next>) : JsGenerator<'Yield, unit, 'Next> = nativeOnly

    /// <summary>Creates a generator with unit input when its sent type is unspecified.</summary>
    member _.Run(body: JsGeneratorCode<'Yield, 'Return, unit>) : JsGenerator<'Yield, 'Return, unit> = nativeOnly

    /// <summary>Creates a one-way generator with unit input and terminal values.</summary>
    member _.Run(body: JsGeneratorCode<'Yield, unit, unit>) : JsGenerator<'Yield, unit, unit> = nativeOnly

    /// <summary>Runs two generator bodies in sequence.</summary>
    member _.Combine
        (first: JsGeneratorCode<'Yield, 'Return, 'Next>, second: JsGeneratorCode<'Yield, 'Return, 'Next>)
        : JsGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Repeats a body while its guard holds.</summary>
    member _.While
        (guard: unit -> bool, body: JsGeneratorCode<'Yield, 'Return, 'Next>)
        : JsGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Runs a body for each value in a sequence.</summary>
    member _.For
        (values: seq<'T>, body: 'T -> JsGeneratorCode<'Yield, 'Return, 'Next>)
        : JsGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Handles exceptions, including exceptions sent through <c>throw</c>.</summary>
    member _.TryWith
        (body: JsGeneratorCode<'Yield, 'Return, 'Next>, handler: exn -> JsGeneratorCode<'Yield, 'Return, 'Next>)
        : JsGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Runs compensation when the generator leaves its protected body.</summary>
    member _.TryFinally
        (body: JsGeneratorCode<'Yield, 'Return, 'Next>, compensation: unit -> unit)
        : JsGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

    /// <summary>Disposes a resource when the generator leaves its body.</summary>
    member _.Using<'Resource when 'Resource :> IDisposable>
        (resource: 'Resource, body: 'Resource -> JsGeneratorCode<'Yield, 'Return, 'Next>)
        : JsGeneratorCode<'Yield, 'Return, 'Next>
        =
        nativeOnly

/// <summary>Provides the native JavaScript generator computation expression.</summary>
[<AutoOpen; Erase>]
module NativeGenerators =
    /// <summary>Creates a native generator with typed yield, return, and next values.</summary>
    /// <example><code lang="fsharp">
    /// let conversation () : JsGenerator&lt;int, string, int> = jsGenerator {
    ///     let! reply = 1
    ///     return string reply
    /// }
    /// </code></example>
    let jsGenerator<'Yield, 'Return, 'Next> : JsGeneratorBuilder<'Yield, 'Return, 'Next> =
        nativeOnly

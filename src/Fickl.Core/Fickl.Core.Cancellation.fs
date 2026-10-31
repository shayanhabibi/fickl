namespace Fickl.Core

open System.Threading
open Fable.Core

/// <summary>Conversions between a <c>CancellationToken</c> and the web <c>AbortSignal</c>, and promise interop that
/// follows either. Targets JavaScript and TypeScript.</summary>
/// <remarks>Cancelling a token aborts its signal with a <c>DOMException</c> named <c>"AbortError"</c>, which an aborted
/// <c>fetch</c> rejects with. Such an error is an <c>OperationCanceledException</c> for <c>:?</c>, and an async
/// awaiting a promise that rejects with it is cancelled. The other way round, an <c>OperationCanceledException</c>
/// is named <c>"AbortError"</c>, as JavaScript code expects of an abort.</remarks>
/// <example><code lang="fsharp">
/// let cts = new CancellationTokenSource()
/// fetch url {| signal = cts.Token.ToAbortSignal() |}
/// let ct = CancellationToken.FromAbortSignal(request.signal)
/// </code></example>
[<AutoOpen>]
module CancellationExtensions =
    type CancellationToken with

        /// <summary>A signal that aborts when this token is cancelled, or one that has aborted if it is.
        /// The same token always gives the same signal.</summary>
        member _.ToAbortSignal() : JS.AbortSignal = nativeOnly

        /// <summary>A token that is cancelled when the signal aborts, or one that is cancelled if it has.
        /// The same signal always gives the same token, and a signal from <c>ToAbortSignal</c> gives its token back.</summary>
        /// <param name="signal">The signal to follow.</param>
        static member FromAbortSignal(signal: JS.AbortSignal) : CancellationToken = nativeOnly

    type Async with

        /// <summary>Awaits a promise, unless the token is cancelled first: the computation is then cancelled
        /// without waiting for the promise.</summary>
        /// <param name="promise">The promise to await. Pass it the token's <c>ToAbortSignal()</c> to stop the work too.</param>
        /// <param name="token">The token that stops the wait.</param>
        static member AwaitPromise(promise: JS.Promise<'T>, token: CancellationToken) : Async<'T> = nativeOnly

        /// <summary>Starts the computation as a promise, cancelled when the signal aborts. A cancelled computation
        /// rejects with an <c>OperationCanceledException</c>, named <c>"AbortError"</c>.</summary>
        /// <param name="workflow">The computation to start.</param>
        /// <param name="signal">The signal that cancels the computation.</param>
        static member StartAsPromise(workflow: Async<'T>, signal: JS.AbortSignal) : JS.Promise<'T> = nativeOnly

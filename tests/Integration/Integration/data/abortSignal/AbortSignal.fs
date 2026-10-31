module AbortSignal

open System.Threading
open Fable.Core
open Fickl.Core

let signalOf (cts: CancellationTokenSource) : JS.AbortSignal = cts.Token.ToAbortSignal()

let tokenOf (signal: JS.AbortSignal) : CancellationToken = CancellationToken.FromAbortSignal(signal)

let timeoutToken (milliseconds: int) = CancellationToken.FromAbortSignal(JS.AbortSignal.timeout milliseconds)

let either (a: JS.AbortSignal) (b: JS.AbortSignal) = JS.AbortSignal.any [| a; b |]

let awaitUntilCancelled (promise: JS.Promise<int>) (token: CancellationToken) = Async.AwaitPromise(promise, token)

let startWithSignal (work: Async<int>) (signal: JS.AbortSignal) = Async.StartAsPromise(work, signal)

let abortNow () =
    let controller = JS.AbortController()
    controller.abort ()
    controller.signal.aborted

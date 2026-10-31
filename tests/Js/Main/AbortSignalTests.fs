module Fable.Tests.AbortSignals

open System
open System.Threading
open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
let private never<'T> () : JS.Promise<'T> = JS.Constructors.Promise.Create(fun _ _ -> ())

let private abortError () : JS.DOMException = JS.DOMException("aborted", "AbortError")

/// The continuation the computation finished with
let private outcome (work: Async<'T>) : Async<string> =
    Async.FromContinuations(fun (ok, _, _) ->
        Async.StartWithContinuations(
            work,
            (fun _ -> ok "success"),
            (fun e -> ok ("error: " + e.Message)),
            (fun _ -> ok "cancelled")
        )
    )
#endif

let tests =
    testList "AbortSignals" [
#if FABLE_COMPILER
        testCase "ToAbortSignal aborts with an AbortError when the token is cancelled" <| fun () ->
            let cts = new CancellationTokenSource()
            let signal = cts.Token.ToAbortSignal()
            equal false signal.aborted
            cts.Cancel()
            equal true signal.aborted
            equal "AbortError" (unbox<string> signal.reason?name)

        testCase "ToAbortSignal gives the same signal for the same token" <| fun () ->
            let cts = new CancellationTokenSource()
            equal true (obj.ReferenceEquals(cts.Token.ToAbortSignal(), cts.Token.ToAbortSignal()))

        testCase "ToAbortSignal of a cancelled token has aborted" <| fun () ->
            let cts = new CancellationTokenSource()
            cts.Cancel()
            equal true (cts.Token.ToAbortSignal().aborted)

        testCase "FromAbortSignal is cancelled when the signal aborts" <| fun () ->
            let controller = JS.AbortController()
            let token = CancellationToken.FromAbortSignal(controller.signal)
            let mutable calls = 0
            use _ = token.Register(fun () -> calls <- calls + 1)
            equal false token.IsCancellationRequested
            controller.abort ()
            controller.abort ()
            equal true token.IsCancellationRequested
            equal 1 calls

        testCase "FromAbortSignal of an aborted signal is cancelled" <| fun () ->
            let token = CancellationToken.FromAbortSignal(JS.AbortSignal.abort ())
            equal true token.IsCancellationRequested

        testCase "A token and its signal convert back to each other" <| fun () ->
            let cts = new CancellationTokenSource()
            let signal = cts.Token.ToAbortSignal()
            equal true (obj.ReferenceEquals(CancellationToken.FromAbortSignal(signal), cts.Token))
            let controller = JS.AbortController()
            let token = CancellationToken.FromAbortSignal(controller.signal)
            equal true (obj.ReferenceEquals(token.ToAbortSignal(), controller.signal))

        testCase "An AbortError is an OperationCanceledException" <| fun () ->
            let result =
                try
                    raise (abortError ())
                    "none"
                with
                | :? OperationCanceledException -> "cancelled"
                | e -> "other: " + e.Message

            equal "cancelled" result

        testCase "Other DOMExceptions are not OperationCanceledExceptions" <| fun () ->
            let result =
                try
                    raise (JS.DOMException("late", "TimeoutError"))
                    "none"
                with
                | :? OperationCanceledException -> "cancelled"
                | :? JS.DOMException as e -> e.name

            equal "TimeoutError" result

        testCase "An OperationCanceledException is named AbortError" <| fun () ->
            let e = OperationCanceledException()
            equal "AbortError" (unbox<string> e?name)

        testCaseAsync "An async awaiting a promise rejected with an AbortError is cancelled" <| fun () -> async {
            let rejected: JS.Promise<int> = JS.Constructors.Promise.Create(fun _ reject -> reject (box (abortError ())))
            let! result = outcome (Async.AwaitPromise rejected)
            equal "cancelled" result
        }

        testCaseAsync "AwaitPromise with a token is cancelled when the token is" <| fun () -> async {
            let cts = new CancellationTokenSource()
            let waiting = outcome (Async.AwaitPromise(never<int> (), cts.Token)) |> Async.StartAsPromise
            cts.Cancel()
            let! result = Async.AwaitPromise waiting
            equal "cancelled" result
        }

        testCaseAsync "AwaitPromise with a token returns the promise's value" <| fun () -> async {
            let cts = new CancellationTokenSource()
            let! value = Async.AwaitPromise(JS.Constructors.Promise.resolve 42, cts.Token)
            equal 42 value
            cts.Cancel()
        }

        testCaseAsync "StartAsPromise with a signal rejects with an AbortError when it aborts" <| fun () -> async {
            let controller = JS.AbortController()
            let started = Async.StartAsPromise(Async.Sleep 10000, controller.signal)
            controller.abort ()
            let settled: JS.Promise<string> = emitJsExpr started "$0.then(() => 'resolved', (e) => e.name)"
            let! name = Async.AwaitPromise settled
            equal "AbortError" name
        }

        testCaseAsync "jsAsync can register on a token converted from a signal" <| fun () -> async {
            let controller = JS.AbortController()
            let token = CancellationToken.FromAbortSignal(controller.signal)
            let mutable calls = 0
            let work = jsAsync {
                use _ = token.Register(fun () -> calls <- calls + 1)
                controller.abort ()
                return calls
            }
            let! during = Async.AwaitPromise work
            equal 1 during
        }
#endif
    ]

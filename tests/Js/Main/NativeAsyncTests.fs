module Fable.Tests.NativeAsync

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
[<AsyncFunction>]
let fetchTwice (promise: JS.Promise<int>) = jsAsync {
    let! first = promise
    let! second = promise
    return first + second
}

[<AsyncFunction>]
let private loops () = jsAsync {
    let mutable total = 0
    for n in [1; 2; 3] do
        let! value = JS.Constructors.Promise.resolve(n)
        total <- total + value
    while total < 10 do
        do! JS.Constructors.Promise.resolve(())
        total <- total + 1
    return total
}

[<AsyncFunction>]
let identityAsync<'T> (value: 'T) = jsAsync { return value }

[<AsyncFunction>]
let private failFast () : JS.Promise<int> = jsAsync { return failwith "fast" }

[<AsyncFunction>]
let addAsync (first: JS.Promise<int>) (second: JS.Promise<int>) = jsAsync {
    let! a = first
    let! b = second
    return a + b
}

[<AsyncFunction>]
let private readReceiver () : JS.Promise<string> = jsAsync {
    do! JS.Constructors.Promise.resolve(())
    return emitJsExpr () "this.tag"
}

let private expressionIdentity<'T> (value: 'T) : JS.Promise<'T> = jsAsync { return value }
let private expressionList<'T> (value: 'T list) : JS.Promise<'T list> = jsAsync { return value }
let private expressionArray<'T> (value: 'T array) : JS.Promise<'T array> = jsAsync { return value }
let private expressionBind<'T> (promise: JS.Promise<'T>) : JS.Promise<'T> = jsAsync {
    let! value = promise
    return value
}
let private expressionReturnFrom<'T> (promise: JS.Promise<'T>) : JS.Promise<'T> = jsAsync { return! promise }

let private higherOrderNativeAsync<'T> () = fun (value: 'T) -> jsAsync { return value }
[<Emit("$0($1)")>]
let private invokeNativeAsync (callback: 'T -> JS.Promise<'T>) (value: 'T) : JS.Promise<'T> = Fable.Core.Util.nativeOnly
#endif

let tests =
    testList "NativeAsync" [
#if FABLE_COMPILER
        testCaseAsync "native async never silently unwraps a Promise stored in an option" <| fun () -> async {
            let payload = JS.Constructors.Promise.resolve(42)
            let computation = jsAsync { return Some payload }
            let! preservedOrRejected = async {
                try
                    let! result = computation |> Async.AwaitPromise
                    match result with
                    | Some value -> return emitJsExpr<bool> (value, payload) "$0 === $1"
                    | None -> return false
                with error -> return error.Message.Contains("resolved")
            }
            preservedOrRejected |> equal true
            let! plain = jsAsync { return Some 42 } |> Async.AwaitPromise
            plain |> equal (Some 42)
            let! boxed = jsAsync { return {| Payload = payload |} } |> Async.AwaitPromise
            emitJsExpr<bool> (boxed.Payload, payload) "$0 === $1" |> equal true
            let! callback = jsAsync { return fun first second -> first + second } |> Async.AwaitPromise
            callback 20 22 |> equal 42
        }

        testCaseAsync "generic native async bodies reject hidden Promise payloads" <| fun () -> async {
            let payload = JS.Constructors.Promise.resolve(42)
            let nested = invokeNativeAsync (higherOrderNativeAsync<obj>()) (box payload)
            let! rejected = async {
                try
                    let! _ = nested |> Async.AwaitPromise
                    return false
                with error -> return error.Message.Contains("resolved")
            }
            rejected |> equal true
        }

        testCaseAsync "generic native async expressions retain scalar and collection types" <| fun () -> async {
            let! scalar = expressionIdentity "value" |> Async.AwaitPromise
            scalar |> equal "value"
            let! values = expressionList [ 20; 22 ] |> Async.AwaitPromise
            values |> equal [ 20; 22 ]
            let! array = expressionArray [| "a"; "b" |] |> Async.AwaitPromise
            array |> equal [| "a"; "b" |]
            let! bound = expressionBind (JS.Constructors.Promise.resolve(42)) |> Async.AwaitPromise
            bound |> equal 42
            let! delegated = expressionReturnFrom (JS.Constructors.Promise.resolve(42)) |> Async.AwaitPromise
            delegated |> equal 42
            let! callback = expressionIdentity (fun first second -> first + second) |> Async.AwaitPromise
            callback 20 22 |> equal 42
        }

        testCaseAsync "native async exports await promises and retain AsyncFunction identity" <| fun () -> async {
            emitJsExpr fetchTwice "$0.constructor.name" |> equal "AsyncFunction"
            let! value = fetchTwice (JS.Constructors.Promise.resolve(21)) |> Async.AwaitPromise
            value |> equal 42
        }

        testCaseAsync "native async computations capture locals and run immediately" <| fun () -> async {
            let mutable started = false
            let offset = 2
            let promise = jsAsync {
                started <- true
                let! value = JS.Constructors.Promise.resolve(40)
                return value + offset
            }
            started |> equal true
            let! value = Async.AwaitPromise promise
            value |> equal 42
        }

        testCaseAsync "native async loops await each iteration" <| fun () -> async {
            let! value = loops() |> Async.AwaitPromise
            value |> equal 10
        }

        testCaseAsync "native async handlers catch rejected promises and run finally" <| fun () -> async {
            let mutable finalized = 0
            let promise = jsAsync {
                try
                    try
                        let! value = JS.Constructors.Promise.reject<int>(System.Exception("rejected"))
                        return value
                    with _ -> return 42
                finally finalized <- finalized + 1
            }
            let! value = Async.AwaitPromise promise
            value |> equal 42
            finalized |> equal 1
        }

        testCaseAsync "native async return bang adopts promises" <| fun () -> async {
            let! value = jsAsync { return! JS.Constructors.Promise.resolve(42) } |> Async.AwaitPromise
            value |> equal 42
        }

        testCaseAsync "native async use disposes after awaiting" <| fun () -> async {
            let mutable disposed = false
            let resource = { new System.IDisposable with member _.Dispose() = disposed <- true }
            let promise = jsAsync {
                use _resource = resource
                let! value = JS.Constructors.Promise.resolve(42)
                disposed |> equal false
                return value
            }
            let! value = Async.AwaitPromise promise
            value |> equal 42
            disposed |> equal true
        }

        testCaseAsync "awaited values remain captured by returned closures" <| fun () -> async {
            let promise = jsAsync {
                let! value = JS.Constructors.Promise.resolve(40)
                return fun offset -> value + offset
            }
            let! callback = Async.AwaitPromise promise
            callback 2 |> equal 42
        }

        testCaseAsync "generic native async exports retain Promise typing" <| fun () -> async {
            let! value = identityAsync "generic" |> Async.AwaitPromise
            value |> equal "generic"
            emitJsExpr identityAsync<string> "$0.constructor.name" |> equal "AsyncFunction"
        }

        testCaseAsync "native async failures before awaiting reject the returned promise" <| fun () -> async {
            let mutable synchronousFailure = false
            let promise =
                try failFast()
                with _ ->
                    synchronousFailure <- true
                    JS.Constructors.Promise.resolve(0)
            synchronousFailure |> equal false
            let! failed = async {
                try
                    let! _ = Async.AwaitPromise promise
                    return false
                with _ -> return true
            }
            failed |> equal true
        }

        testCaseAsync "native async exports retain multiple arguments and function identity" <| fun () -> async {
            emitJsExpr addAsync "$0.constructor.name" |> equal "AsyncFunction"
            let! value = addAsync (JS.Constructors.Promise.resolve(20)) (JS.Constructors.Promise.resolve(22)) |> Async.AwaitPromise
            value |> equal 42
        }

        testCaseAsync "native async exports retain a receiver across awaiting" <| fun () -> async {
            let promise: JS.Promise<string> = emitJsExpr readReceiver "$0.call({ tag: 'receiver' })"
            let! value = Async.AwaitPromise promise
            value |> equal "receiver"
        }

        testCaseAsync "native async expressions capture an enclosing callback receiver" <| fun () -> async {
            let callback = Fickl.Core.JsInterop.withThis (fun (_self: obj) () -> jsAsync {
                do! JS.Constructors.Promise.resolve(())
                return emitJsExpr<string> () "this.tag"
            })
            let promise: JS.Promise<string> = emitJsExpr callback "$0.call({ tag: 'enclosing' })"
            let! value = Async.AwaitPromise promise
            value |> equal "enclosing"
        }

        testCaseAsync "return bang rejection is caught within native async handlers" <| fun () -> async {
            let promise = jsAsync {
                try return! JS.Constructors.Promise.reject<int>(System.Exception("rejected"))
                with _ -> return 42
            }
            let! value = Async.AwaitPromise promise
            value |> equal 42
        }
#endif
    ]

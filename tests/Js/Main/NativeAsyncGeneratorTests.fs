module Fable.Tests.NativeAsyncGenerators

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
[<AsyncGeneratorFunction>]
let conversation (request: int) : JsAsyncGenerator<int, string, int> = jsAsyncGenerator {
    let! ready = JS.Constructors.Promise.resolve(request)
    let! reply = jsYield ready
    return string reply
}

let private yielded (result: JsGeneratorResult<int, string>) =
    match result with JsGeneratorResult.Yielded result -> result.value | _ -> failwith "Expected yield"
let private terminal (result: JsGeneratorResult<int, string>) =
    match result with JsGeneratorResult.Returned result -> result.value | _ -> failwith "Expected return"

let private opaqueYield<'T> (value: 'T) : JsAsyncGenerator<'T, unit, unit> = jsAsyncGenerator { yield value }
let private opaqueReturn<'T> (value: 'T) : JsAsyncGenerator<int, 'T, unit> = jsAsyncGenerator { return value }
let private opaqueDelegate<'T> (values: seq<'T>) : JsAsyncGenerator<'T, unit, unit> = jsAsyncGenerator { yield! values }
#endif

let tests =
    testList "NativeAsyncGenerators" [
#if FABLE_COMPILER
    testCaseAsync "checked async delegation preserves Promise-valued sent objects" <| fun () -> async {
        let payload = JS.Constructors.Promise.resolve(42)
        let child: JsAsyncGenerator<obj, obj, JS.Promise<int>> = jsAsyncGenerator {
            let! (sent: JS.Promise<int>) = jsYield (box 1)
            return box (emitJsExpr<bool> (sent, payload) "$0 === $1")
        }
        let parent: JsAsyncGenerator<obj, obj, JS.Promise<int>> = jsAsyncGenerator { return! child }
        let! _ = parent.next() |> Async.AwaitPromise
        let! result = parent.next(payload) |> Async.AwaitPromise
        match result with
        | JsGeneratorResult.Returned result -> unbox<bool> result.value |> equal true
        | _ -> failwith "Expected return"
    }
    testCaseAsync "checked async delegation forwards injected errors" <| fun () -> async {
        let child: JsAsyncGenerator<obj, obj, unit> = jsAsyncGenerator {
            try yield box 1; return box "normal"
            with _ -> yield box "caught"; return box "terminal"
        }
        let parent: JsAsyncGenerator<obj, obj, unit> = jsAsyncGenerator { return! child }
        let! _ = parent.next() |> Async.AwaitPromise
        let! result = parent.throw(System.Exception("injected")) |> Async.AwaitPromise
        match result with
        | JsGeneratorResult.Yielded result -> unbox<string> result.value |> equal "caught"
        | _ -> failwith "Expected yield"
        let! result = parent.next() |> Async.AwaitPromise
        match result with
        | JsGeneratorResult.Returned result -> unbox<string> result.value |> equal "terminal"
        | _ -> failwith "Expected return"
    }
    testCaseAsync "checked async delegation forwards early return and child cleanup" <| fun () -> async {
        let mutable closed = false
        let child: JsAsyncGenerator<obj, obj, unit> = jsAsyncGenerator {
            try yield box 1; return box "normal"
            finally closed <- true
        }
        let parent: JsAsyncGenerator<obj, obj, unit> = jsAsyncGenerator { return! child }
        let! _ = parent.next() |> Async.AwaitPromise
        let! result = parent.``return``(box "closed") |> Async.AwaitPromise
        match result with
        | JsGeneratorResult.Returned result -> unbox<string> result.value |> equal "closed"
        | _ -> failwith "Expected return"
        closed |> equal true
    }
    testCaseAsync "async delegation rejects opaque Promise yields and closes synchronous sources" <| fun () -> async {
        let mutable closed = false
        let values = seq {
            try yield box (JS.Constructors.Promise.resolve(42))
            finally closed <- true
        }
        let generator = opaqueDelegate values
        let! rejected = async {
            try
                let! _ = generator.next() |> Async.AwaitPromise
                return false
            with error -> return error.Message.Contains("resolved")
        }
        rejected |> equal true
        closed |> equal true
    }
    testCaseAsync "async delegation rejects opaque Promise yields and awaits asynchronous source closure" <| fun () -> async {
        let mutable closed = false
        let source: JS.AsyncIterable<obj> = emitJsExpr (JS.Constructors.Promise.resolve(42), fun () -> closed <- true) "({ [Symbol.asyncIterator]() { return Object.freeze({ next() { return Promise.resolve({ value: $0, done: false }); }, async return() { await Promise.resolve(); $1(); return { value: undefined, done: true }; } }); } })"
        let generator: JsAsyncGenerator<obj, unit, unit> = jsAsyncGenerator { yield! source }
        let! rejected = async {
            try
                let! _ = generator.next() |> Async.AwaitPromise
                return false
            with error -> return error.Message.Contains("resolved")
        }
        rejected |> equal true
        closed |> equal true
    }
    testCaseAsync "async generators reject Promise objects hidden in generic yields" <| fun () -> async {
        let generator = opaqueYield (box (JS.Constructors.Promise.resolve(42)))
        let! rejected = async {
            try
                let! _ = generator.next() |> Async.AwaitPromise
                return false
            with error -> return error.Message.Contains("resolved")
        }
        rejected |> equal true
    }
    testCaseAsync "async generators reject Promise objects hidden in generic terminal values" <| fun () -> async {
        let generator = opaqueReturn (box (JS.Constructors.Promise.resolve(42)))
        let! rejected = async {
            try
                let! _ = generator.next() |> Async.AwaitPromise
                return false
            with error -> return error.Message.Contains("resolved")
        }
        rejected |> equal true
    }
    testCaseAsync "async generators await promises and exchange typed values" <| fun () -> async {
        emitJsExpr conversation "$0.constructor.name" |> equal "AsyncGeneratorFunction"
        let generator = conversation 21
        emitJsExpr generator "Object.prototype.toString.call($0)" |> equal "[object AsyncGenerator]"
        let! first = generator.next() |> Async.AwaitPromise
        yielded first |> equal 21
        let! last = generator.next(42) |> Async.AwaitPromise
        terminal last |> equal "42"
    }
    testCaseAsync "async generator execution and promise bindings are lazy" <| fun () -> async {
        let mutable steps = 0
        let generator: JsAsyncGenerator<int, string, unit> = jsAsyncGenerator {
            steps <- steps + 1
            let! ready = JS.Constructors.Promise.resolve(21)
            yield ready
            steps <- steps + 1
            let! doneValue = JS.Constructors.Promise.resolve("done")
            return doneValue
        }
        steps |> equal 0
        let! first = generator.next() |> Async.AwaitPromise
        yielded first |> equal 21
        steps |> equal 1
        let! last = generator.next() |> Async.AwaitPromise
        terminal last |> equal "done"
        steps |> equal 2
    }
    testCaseAsync "native async consumers iterate async sources" <| fun () -> async {
        let generator = jsAsyncGenerator { yield 20; yield 22 }
        let mutable total = 0
        do! (jsAsync {
            for value in generator do
                let! ready = JS.Constructors.Promise.resolve(value)
                total <- total + ready
        } |> Async.AwaitPromise)
        total |> equal 42
    }
    testCaseAsync "async iterator return is awaited when a consumer throws" <| fun () -> async {
        let mutable closed = false
        let source: JS.AsyncIterable<int> = emitJsExpr (fun () -> closed <- true) "({ [Symbol.asyncIterator]() { return { next: async () => ({ value: 1, done: false }), return: async () => { await Promise.resolve(); $0(); return { value: undefined, done: true }; } }; } })"
        do! (jsAsync {
            try
                for _value in source do failwith "stop"
            with _ -> closed |> equal true
        } |> Async.AwaitPromise)
    }
    testCaseAsync "async generators delegate and retain terminal values" <| fun () -> async {
        let child: JsAsyncGenerator<int, string, unit> = jsAsyncGenerator { yield 42; return "child" }
        let parent: JsAsyncGenerator<int, string, unit> = jsAsyncGenerator { yield! [1; 2]; return! child }
        let! first = parent.next() |> Async.AwaitPromise
        yielded first |> equal 1
        let! second = parent.next() |> Async.AwaitPromise
        yielded second |> equal 2
        let! third = parent.next() |> Async.AwaitPromise
        yielded third |> equal 42
        let! last = parent.next() |> Async.AwaitPromise
        terminal last |> equal "child"
    }
    testCaseAsync "async generator return runs finally exactly once" <| fun () -> async {
        let mutable finalized = 0
        let generator: JsAsyncGenerator<int, string, unit> = jsAsyncGenerator {
            try yield 1; yield 2; return "done"
            finally finalized <- finalized + 1
        }
        let! _ = generator.next() |> Async.AwaitPromise
        let! last = generator.``return``("closed") |> Async.AwaitPromise
        terminal last |> equal "closed"
        let! _ = generator.``return``("again") |> Async.AwaitPromise
        finalized |> equal 1
    }
    testCaseAsync "async generator for await closes its source on early return" <| fun () -> async {
        let mutable closed = false
        let source: JS.AsyncIterable<int> = emitJsExpr (fun () -> closed <- true) "({ [Symbol.asyncIterator]() { return { next: async () => ({ value: 42, done: false }), return: async () => { await Promise.resolve(); $0(); return { value: undefined, done: true }; } }; } })"
        let generator: JsAsyncGenerator<int, string, unit> = jsAsyncGenerator {
            for value in source do
                yield value
                return "early"
            return "empty"
        }
        let! first = generator.next() |> Async.AwaitPromise
        yielded first |> equal 42
        let! last = generator.next() |> Async.AwaitPromise
        terminal last |> equal "early"
        closed |> equal true
    }
    testCaseAsync "async iteration preserves curried function payloads" <| fun () -> async {
        let source = jsAsyncGenerator { yield (fun first second -> first + second) }
        let mutable total = 0
        do! (jsAsync {
            for callback in source do
                total <- callback 20 22
        } |> Async.AwaitPromise)
        total |> equal 42
    }
    testCaseAsync "async generators await asynchronous resource cleanup" <| fun () -> async {
        let mutable disposed = false
        let resource = { new JsAsyncDisposable with
            member _.``Symbol.asyncDispose``() = jsAsync {
                do! JS.Constructors.Promise.resolve(())
                disposed <- true
            } }
        let generator: JsAsyncGenerator<int, string, unit> = jsAsyncGenerator {
            use _resource = resource
            yield 42
            return "done"
        }
        let! _ = generator.next() |> Async.AwaitPromise
        disposed |> equal false
        let! _ = generator.``return``("closed") |> Async.AwaitPromise
        disposed |> equal true
    }
    testCaseAsync "async generators catch injected errors" <| fun () -> async {
        let generator: JsAsyncGenerator<int, string, unit> = jsAsyncGenerator {
            try yield 1; return "normal"
            with _ -> yield 42; return "caught"
        }
        let! _ = generator.next() |> Async.AwaitPromise
        let! recovered = generator.throw(System.Exception("injected")) |> Async.AwaitPromise
        yielded recovered |> equal 42
        let! last = generator.next() |> Async.AwaitPromise
        terminal last |> equal "caught"
    }
    testCaseAsync "async generators preserve an enclosing receiver" <| fun () -> async {
        let callback = Fickl.Core.JsInterop.withThis (fun (_self: obj) () ->
            let generator: JsAsyncGenerator<string, unit, unit> = jsAsyncGenerator {
                do! JS.Constructors.Promise.resolve(())
                yield emitJsExpr<string> () "this.tag"
            }
            generator)
        let generator: JsAsyncGenerator<string, unit, unit> = emitJsExpr callback "$0.call({ tag: 'receiver' })"
        let! result = generator.next() |> Async.AwaitPromise
        match result with
        | JsGeneratorResult.Yielded result -> result.value |> equal "receiver"
        | _ -> failwith "Expected yield"
    }
    testCaseAsync "async generators preserve promise-valued sent inputs" <| fun () -> async {
        let generator: JsAsyncGenerator<int, int, JS.Promise<int>> = jsAsyncGenerator {
            let! (response: JS.Promise<int>) = jsYield 1
            emitJsExpr<bool> response "typeof $0.then === 'function'" |> equal true
            let! value = response
            return value
        }
        let! _ = generator.next() |> Async.AwaitPromise
        let! result = generator.next(JS.Constructors.Promise.resolve(42)) |> Async.AwaitPromise
        match result with
        | JsGeneratorResult.Returned result -> result.value |> equal 42
        | _ -> failwith "Expected return"
    }
#endif
    ]

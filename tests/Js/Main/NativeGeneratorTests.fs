module Fable.Tests.NativeGenerators

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
let private yielded (result: JsGeneratorResult<int, string>) =
    match result with JsGeneratorResult.Yielded result -> result.value | _ -> failwith "Expected yielded value"
let private terminal (result: JsGeneratorResult<int, string>) =
    match result with JsGeneratorResult.Returned result -> result.value | _ -> failwith "Expected terminal value"

[<GeneratorFunction>]
let conversation (request: int) : JsGenerator<int, string, int> = jsGenerator {
    let! reply = request
    yield reply + 1
    return string (reply + 2)
}

let private createWithoutAttribute () : JsGenerator<int, string, unit> = jsGenerator {
    yield 1
    return "done"
}

type Branch = First | Second | Third

[<GeneratorFunction>]
let private classify branch : JsGenerator<int, string, unit> = jsGenerator {
    match branch with
    | First -> yield 1; return "first"
    | Second -> yield 2; return "second"
    | Third -> return "third"
}
#endif

let tests =
    testList "NativeGenerators" [
#if FABLE_COMPILER
        testCase "one-way generators infer unit input and terminal types" <| fun () ->
            let generator = jsGenerator { yield 1; yield 2 }
            generator |> Seq.toArray |> equal [|1; 2|]

        testCase "native generators exchange values and return a terminal result" <| fun () ->
            let generator = conversation 1
            let first = generator.next()
            first.``done`` |> equal false
            yielded first |> equal 1
            yielded (generator.next(40)) |> equal 41
            let last = generator.next(0)
            last.``done`` |> equal true
            terminal last |> equal "42"
            emitJsExpr conversation "$0.constructor.name" |> equal "GeneratorFunction"
            emitJsExpr generator "Object.prototype.toString.call($0)" |> equal "[object Generator]"

        testCase "native generator computations are lazy" <| fun () ->
            let mutable started = false
            let generator: JsGenerator<int, string, int> = jsGenerator {
                started <- true
                yield 1
                return "done"
            }
            started |> equal false
            yielded (generator.next()) |> equal 1
            started |> equal true

        testCase "native return closes generators and runs finally once" <| fun () ->
            let mutable finalized = 0
            let generator: JsGenerator<int, string, int> = jsGenerator {
                try
                    yield 1
                    yield 2
                    return "done"
                finally finalized <- finalized + 1
            }
            generator.next() |> ignore
            terminal (generator.``return``("closed")) |> equal "closed"
            generator.``return``("again") |> ignore
            finalized |> equal 1

        testCase "native throw enters generator handlers" <| fun () ->
            let generator: JsGenerator<int, string, int> = jsGenerator {
                try
                    yield 1
                    return "normal"
                with _ ->
                    yield 9
                    return "caught"
            }
            generator.next() |> ignore
            yielded (generator.throw(System.Exception("injected"))) |> equal 9
            terminal (generator.next()) |> equal "caught"

        testCase "return bang delegates sent and terminal values" <| fun () ->
            let generator: JsGenerator<int, string, int> = jsGenerator {
                yield 0
                return! conversation 1
            }
            yielded (generator.next()) |> equal 0
            yielded (generator.next()) |> equal 1
            yielded (generator.next(40)) |> equal 41
            terminal (generator.next()) |> equal "42"

        testCase "native generator loops and yield bang are iterable" <| fun () ->
            let generator: JsGenerator<int, unit, unit> = jsGenerator {
                yield! [1; 2]
                for value in [3; 4] do yield value
                let mutable value = 5
                while value < 7 do
                    yield value
                    value <- value + 1
            }
            generator |> Seq.toArray |> equal [|1; 2; 3; 4; 5; 6|]

        testCase "closing a native generator disposes its FSharp resource" <| fun () ->
            let mutable disposed = 0
            let resource = { new System.IDisposable with member _.Dispose() = disposed <- disposed + 1 }
            let generator: JsGenerator<int, string, int> = jsGenerator {
                use _resource = resource
                yield 1
                return "done"
            }
            generator.next() |> ignore
            disposed |> equal 0
            generator.``return``("closed") |> ignore
            disposed |> equal 1

        testCase "short sequence enumeration closes native generators" <| fun () ->
            let mutable finalized = false
            let generator: JsGenerator<int, unit, unit> = jsGenerator {
                try
                    yield 1
                    yield 2
                finally finalized <- true
            }
            generator |> Seq.take 1 |> Seq.toArray |> equal [|1|]
            finalized |> equal true

        testCase "generator returns can exit a loop early" <| fun () ->
            let generator: JsGenerator<int, string, unit> = jsGenerator {
                for value in [1; 2; 3] do
                    if value = 2 then return "early"
                    yield value
                return "late"
            }
            yielded (generator.next()) |> equal 1
            terminal (generator.next()) |> equal "early"

        testCase "generator next accepts an explicit unit sent value" <| fun () ->
            let generator: JsGenerator<int, string, unit> = jsGenerator {
                let! _reply = 1
                return "done"
            }
            generator.next() |> ignore
            terminal (generator.next(())) |> equal "done"

        testCase "generators retain curried sent and terminal function values" <| fun () ->
            let generator: JsGenerator<int, (int -> int -> int), (int -> int -> int)> = jsGenerator {
                let! callback = 1
                return callback
            }
            generator.next() |> ignore
            match generator.next(fun a b -> a + b) with
            | JsGeneratorResult.Returned result -> result.value 20 22 |> equal 42
            | _ -> failwith "Expected terminal callback"

        testCase "generators retain curried yielded function values" <| fun () ->
            let generator: JsGenerator<(int -> int -> int), int, unit> = jsGenerator {
                yield (fun a b -> a + b)
                return 0
            }
            match generator.next() with
            | JsGeneratorResult.Yielded result -> result.value 20 22 |> equal 42
            | _ -> failwith "Expected yielded callback"

        testCase "generator expressions retain their enclosing receiver" <| fun () ->
            let callback = Fickl.Core.JsInterop.withThis (fun (_self: obj) () ->
                let generator: JsGenerator<string, unit, unit> = jsGenerator {
                    yield emitJsExpr<string> () "this.tag"
                }
                generator)
            let generator: JsGenerator<string, unit, unit> = emitJsExpr callback "$0.call({ tag: 'receiver' })"
            match generator.next() with
            | JsGeneratorResult.Yielded result -> result.value |> equal "receiver"
            | _ -> failwith "Expected receiver value"
            yielded (createWithoutAttribute().next()) |> equal 1

        testCase "exhaustive generator matches return terminal values" <| fun () ->
            let first = classify First
            yielded (first.next()) |> equal 1
            terminal (first.next()) |> equal "first"
            terminal ((classify Third).next()) |> equal "third"

        testCase "natural native iterator exhaustion does not call return" <| fun () ->
            let mutable closed = 0
            let source: seq<int> = emitJsExpr (fun () -> closed <- closed + 1) "Object({ [Symbol.iterator]() { let emitted = false; return { next() { if (emitted) return { done: 1, value: undefined }; emitted = true; return { done: false, value: 1 }; }, return() { $0(); return { done: true, value: undefined }; } }; } })"
            source |> Seq.toArray |> equal [|1|]
            closed |> equal 0
            source |> Seq.take 1 |> Seq.toArray |> equal [|1|]
            closed |> equal 1

        testCase "yield delegation closes a suspended FSharp sequence" <| fun () ->
            let mutable closed = 0
            let source = seq {
                try
                    yield 1
                    yield 2
                finally closed <- closed + 1
            }
            let generator: JsGenerator<int, string, unit> = jsGenerator {
                yield! source
                return "done"
            }
            generator.next() |> ignore
            generator.``return``("closed") |> ignore
            closed |> equal 1
#endif
    ]

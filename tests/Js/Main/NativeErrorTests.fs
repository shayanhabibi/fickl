module Fable.Tests.NativeErrors

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

[<Fickl.Core.JsError>]
exception Boom of code: int

exception Plain of code: int

let private raiseBoom (code: int) : int = raise (Boom code)

#if FABLE_COMPILER
let private throwJs (statement: string) : unit = emitJsStatement statement "eval($0)"

let private classify (statement: string) =
    try
        throwJs statement
        "none"
    with
    | :? JS.TypeError as e -> "TypeError: " + e.message
    | :? JS.RangeError as e -> "RangeError: " + e.message
    | :? JS.SyntaxError -> "SyntaxError"
    | :? JS.AggregateError as e -> $"AggregateError: {e.errors.Length}"
    | :? JS.DOMException as e when e.name = "AbortError" -> "aborted"
    | :? JS.Error as e -> "Error: " + e.name
    | JS.Thrown value -> $"thrown {jsTypeof value}"
    | Boom code -> $"Boom {code}"
    | e -> "exn: " + e.Message
#endif

let tests =
    testList "NativeErrors" [
        testCase "JsError exceptions are raised and matched as F# exceptions" <| fun () ->
            let result =
                try
                    raiseBoom 3
                with
                | Failure _ -> -1
                | Boom code -> code

            equal 3 result

        testCase "JsError exceptions keep structural equality" <| fun () ->
            equal (Boom 3) (Boom 3)
            notEqual (Boom 3) (Boom 4)
            equal (hash (Boom 3)) (hash (Boom 3))

        testCase "JsError exceptions are exceptions with a stack trace" <| fun () ->
            let error =
                try
                    raiseBoom 3 |> ignore
                    None
                with e ->
                    Some e

            match error with
            | Some(:? exn as e) -> equal false (isNull e.StackTrace)
            | _ -> failwith "expected an exception"

        testCase "Built-in exceptions keep their inner exception" <| fun () ->
            let inner = exn "inner"
            let error = System.InvalidOperationException("load failed", inner)
            error.Message |> equal "load failed"
            obj.ReferenceEquals(error.InnerException, inner) |> equal true
            System.NotSupportedException("no", inner).InnerException.Message |> equal "inner"
            System.Collections.Generic.KeyNotFoundException("missing", inner).InnerException.Message |> equal "inner"
            System.ArgumentNullException("null", inner).InnerException.Message |> equal "inner"
            System.ArgumentNullException("null", inner).Message |> equal "null"
            System.ArgumentOutOfRangeException("range", inner).InnerException.Message |> equal "inner"
            isNull (System.InvalidOperationException("alone")).InnerException |> equal true
            isNull (System.InvalidOperationException()).InnerException |> equal true
            isNull (exn "alone").InnerException |> equal true

            let caught =
                try
                    raise error
                with
                | :? System.InvalidOperationException as e -> e.InnerException.Message
                | _ -> "other"

            caught |> equal "inner"

        testCase "GetBaseException follows the inner exceptions" <| fun () ->
            let inner = exn "inner"
            let error = System.Exception("outer", System.InvalidOperationException("middle", inner))
            obj.ReferenceEquals(error.GetBaseException(), inner) |> equal true
            obj.ReferenceEquals(inner.GetBaseException(), inner) |> equal true

        testCase "AggregateException holds its inner exceptions" <| fun () ->
            let first = exn "a"
            let error = System.AggregateException("many", [| first; exn "b" |])
            error.InnerExceptions.Count |> equal 2
            error.InnerExceptions[1].Message |> equal "b"
            error.InnerExceptions |> Seq.map _.Message |> String.concat "," |> equal "a,b"
            error.Message |> equal "many (a) (b)"
            obj.ReferenceEquals(error.InnerException, first) |> equal true
            System.AggregateException("many", exn "a", exn "b").InnerExceptions.Count |> equal 2
            System.AggregateException("one", first).InnerExceptions.Count |> equal 1
            System.AggregateException("one", first).InnerException.Message |> equal "a"
            System.AggregateException("list", [ exn "a"; exn "b" ]).Message |> equal "list (a) (b)"
            System.AggregateException(seq { exn "a" }).Message |> equal "One or more errors occurred. (a)"
            System.AggregateException([| exn "a"; exn "b" |]).InnerExceptions.Count |> equal 2
            System.AggregateException("none").InnerExceptions.Count |> equal 0
            System.AggregateException("none").Message |> equal "none"
            isNull (System.AggregateException()).InnerException |> equal true

        testCase "AggregateException is caught by type" <| fun () ->
            let classify (error: exn) =
                try
                    raise error
                with
                | :? System.AggregateException as e -> e.InnerExceptions |> Seq.map _.Message |> String.concat ","
                | e -> "other: " + e.Message

            classify (System.AggregateException("many", exn "a", exn "b")) |> equal "a,b"
            classify (exn "plain") |> equal "other: plain"
            classify (System.InvalidOperationException("op", exn "inner")) |> equal "other: op"

        testCase "AggregateException.GetBaseException stops at several errors" <| fun () ->
            let leaf = exn "leaf"
            let nested = System.AggregateException("outer", System.AggregateException("inner", leaf))
            obj.ReferenceEquals(nested.GetBaseException(), leaf) |> equal true
            let several = System.AggregateException("outer", exn "a", exn "b")
            obj.ReferenceEquals(several.GetBaseException(), several) |> equal true

#if FABLE_COMPILER
        testCase "JsError exceptions are native Errors" <| fun () ->
            let error = Boom 3 :> exn
            emitJsExpr error "$0 instanceof Error" |> equal true
            emitJsExpr error "typeof $0.stack" |> equal "string"
            emitJsExpr error "Object.keys($0).includes('stack')" |> equal false

        testCase "JS errors are caught by type" <| fun () ->
            classify "null.x" |> fun s -> s.StartsWith "TypeError: " |> equal true
            classify "throw new TypeError('bad')" |> equal "TypeError: bad"
            classify "throw new RangeError('far')" |> equal "RangeError: far"
            classify "JSON.parse('{')" |> equal "SyntaxError"
            classify "undeclaredVariable" |> equal "Error: ReferenceError"
            classify "throw new AggregateError([1, 2], 'many')" |> equal "AggregateError: 2"
            classify "throw new DOMException('stop', 'AbortError')" |> equal "aborted"
            classify "throw new DOMException('gone', 'NotFoundError')" |> equal "Error: NotFoundError"

        testCase "JS error bindings construct the native errors" <| fun () ->
            let error = JS.RangeError("far")
            emitJsExpr error "$0 instanceof RangeError" |> equal true
            error.name |> equal "RangeError"
            error.message |> equal "far"
            (JS.DOMException("stop", "AbortError")).name |> equal "AbortError"
            (JS.AggregateError([| box 1 |], "one")).errors.Length |> equal 1

            let caught =
                try
                    raise (JS.TypeError "bad")
                with
                | :? JS.TypeError as e -> e.Message
                | _ -> "other"

            caught |> equal "bad"

        testCase "JS.Thrown matches thrown values that are not errors" <| fun () ->
            classify "throw 'boom'" |> equal "thrown string"
            classify "throw 42" |> equal "thrown number"
            classify "throw { code: 1 }" |> equal "thrown object"
            classify "throw undefined" |> equal "thrown undefined"
            classify "throw null" |> equal "thrown object"
            classify "throw new Error('plain')" |> equal "Error: Error"

        testCase "JS.Thrown does not match F# exceptions" <| fun () ->
            let matchThrown (e: exn) =
                match e with
                | JS.Thrown _ -> true
                | _ -> false

            matchThrown (exn "x") |> equal false
            matchThrown (Boom 3) |> equal false
            matchThrown (Plain 3) |> equal false
            matchThrown (System.ArgumentException "x") |> equal false

        testCase "JS.Thrown passes the thrown value as it was thrown" <| fun () ->
            let value =
                try
                    throwJs "throw 'boom'"
                    null
                with JS.Thrown value ->
                    value

            value |> equal (box "boom")

        testCase "JS.Error does not catch F# exceptions by default" <| fun () ->
            let caught =
                try
                    raise (Plain 3)
                with
                | :? JS.Error -> "js"
                | _ -> "fsharp"

            caught |> equal "fsharp"

        testCase "Message of a thrown value that is not an object is its string" <| fun () ->
            let message statement =
                try
                    throwJs statement
                    "none"
                with e ->
                    e.Message

            message "throw 'boom'" |> equal "boom"
            message "throw 42" |> equal "42"
            message "throw undefined" |> equal "undefined"
            message "throw null" |> equal "null"
            message "throw new TypeError('bad')" |> equal "bad"
            message "throw { message: 'object' }" |> equal "object"

        testCase "Other F# exceptions are not native Errors by default" <| fun () ->
            emitJsExpr (Plain 3 :> exn) "$0 instanceof Error" |> equal false
            emitJsExpr (exn "message") "$0 instanceof Error" |> equal false

        testCase "An inner exception is the cause of the JS error" <| fun () ->
            let inner = exn "inner"
            emitJsExpr (exn ("outer", inner), inner) "$0.cause === $1" |> equal true
            emitJsExpr (System.InvalidOperationException("outer", inner), inner) "$0.cause === $1" |> equal true
            emitJsExpr (System.ArgumentException("outer", inner), inner) "$0.cause === $1" |> equal true
            emitJsExpr (exn "alone") "'cause' in $0" |> equal false
            emitJsExpr (exn ("outer", inner)) "Object.keys($0).includes('cause')" |> equal false

        testCase "The cause of a JS error is its InnerException" <| fun () ->
            let inner statement =
                try
                    throwJs statement
                    "none"
                with e ->
                    if isNull e.InnerException then
                        "null"
                    else
                        e.InnerException.Message

            inner "throw new Error('outer', { cause: new TypeError('inner') })" |> equal "inner"
            inner "throw new Error('outer')" |> equal "null"
            inner "throw 'boom'" |> equal "null"
            inner "throw undefined" |> equal "null"

            let baseMessage =
                try
                    throwJs "throw new Error('a', { cause: new Error('b', { cause: new Error('c') }) })"
                    "none"
                with e ->
                    e.GetBaseException().Message

            baseMessage |> equal "c"

        testCase "A JS AggregateError is caught as an AggregateException" <| fun () ->
            let messages statement =
                try
                    throwJs statement
                    "none"
                with
                | :? System.AggregateException as e ->
                    let messages = e.InnerExceptions |> Seq.map _.Message |> String.concat ","
                    $"{e.Message}: {messages} ({e.InnerExceptions.Count})"
                | e -> "other: " + e.Message

            messages "throw new AggregateError([new Error('x'), new TypeError('y')], 'agg')" |> equal "agg: x,y (2)"
            messages "throw new Error('plain')" |> equal "other: plain"

        testCase "AggregateException has the errors of an AggregateError" <| fun () ->
            let first = exn "a"
            let error = System.AggregateException("many", first, exn "b")
            emitJsExpr error "$0.errors.length" |> equal 2
            emitJsExpr (error, first) "$0.errors[0] === $1 && $0.cause === $1" |> equal true
#endif
    ]

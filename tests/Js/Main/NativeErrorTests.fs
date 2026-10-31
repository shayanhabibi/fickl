module Fable.Tests.NativeErrors

open Fable.Core.JsInterop
open Util.Testing

[<Fickl.Core.JsError>]
exception Boom of code: int

exception Plain of code: int

let private raiseBoom (code: int) : int = raise (Boom code)

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

#if FABLE_COMPILER
        testCase "JsError exceptions are native Errors" <| fun () ->
            let error = Boom 3 :> exn
            emitJsExpr error "$0 instanceof Error" |> equal true
            emitJsExpr error "typeof $0.stack" |> equal "string"
            emitJsExpr error "Object.keys($0).includes('stack')" |> equal false

        testCase "Other F# exceptions are not native Errors by default" <| fun () ->
            emitJsExpr (Plain 3 :> exn) "$0 instanceof Error" |> equal false
            emitJsExpr (exn "message") "$0 instanceof Error" |> equal false
#endif
    ]

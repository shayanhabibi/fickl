module Fable.Tests.ImportAttributes

open Util.Testing
open Fable.Tests.Util

#if FABLE_COMPILER
open Fable.Core
open Fickl.Core

type Data =
    abstract name: string
    abstract items: int[]

[<ImportDefault("./js/importAttributes.json"); ImportWith("type", "json")>]
let data: Data = jsNative

[<Import("default", "./js/importAttributes.json"); ImportWith("type", "json")>]
let sameData: Data = jsNative
#endif

let tests =
    testList "ImportAttributes" [
#if FABLE_COMPILER
        testCase "a JSON module is imported with type json" <| fun () ->
            data.name |> equal "fickl"
            data.items |> equal [| 1; 2; 3 |]

        testCase "imports of a path with the same attributes share the module" <| fun () ->
            obj.ReferenceEquals(data, sameData) |> equal true

        testCaseAsync "a dynamic import passes its attributes" <| fun () -> async {
            let! json =
                JsInterop.importDynamicWith<{| ``default``: Data |}> "data:application/json,{\"name\":\"dynamic\"}" [ "type", "json" ]
                |> Async.AwaitPromise

            json.``default``.name |> equal "dynamic"
        }
#endif
    ]

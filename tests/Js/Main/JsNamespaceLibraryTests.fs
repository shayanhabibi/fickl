module Fable.Tests.JsNamespaceLibrary

open Fable.Core.JS
open Util.Testing

let tests =
    testList "JsNamespaceLibrary" [
        testCase "Inline helpers of a library in Fable.Core.JS are inlined" <| fun () ->
            FicklTestLibrary.double 21 |> equal 42

        testCase "Classes of a library in Fable.Core.JS are not globals" <| fun () ->
            let counter = FicklTestLibrary.Counter(40)
            counter.Next() |> ignore
            counter.Next() |> equal 42
    ]

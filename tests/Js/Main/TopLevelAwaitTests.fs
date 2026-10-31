module Fable.Tests.TopLevelAwait

open Util.Testing
open Fable.Tests.Util

#if FABLE_COMPILER
open Fable.Core
open Fickl.Core

let mutable private resolvedBefore = false

let private delayed: string =
    JS.awaitTopLevel (
        JS.Constructors.Promise.Create<string>(fun resolve _ ->
            JS.setTimeout
                (fun () ->
                    resolvedBefore <- true
                    resolve (box "late")
                )
                10
            |> ignore
        )
    )

// Runs after the await above, as the module's code is sequential
let private seenAfter = resolvedBefore

let private answer: int = JS.awaitTopLevel (JS.Constructors.Promise.resolve 42)

let private readAnswer () = answer + 1
#endif

let tests =
    testList "TopLevelAwait" [
#if FABLE_COMPILER
        testCase "a module value has the resolved value" <| fun () ->
            answer |> equal 42
            delayed |> equal "late"

        testCase "the module waits for the promise before running the rest" <| fun () ->
            seenAfter |> equal true

        testCase "functions read the awaited value" <| fun () ->
            readAnswer () |> equal 43
#endif
    ]

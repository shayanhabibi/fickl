module Fable.Tests.TaggedTemplates

open System
open Util.Testing
open Fable.Tests.Util

#if FABLE_COMPILER
open Fickl.Core

type Tagged =
    abstract strings: JS.ReadonlyArray<string>
    abstract values: obj[]

[<Fable.Core.Import("tag", "./js/taggedTemplates.js"); TaggedTemplate>]
let tag (template: FormattableString) : Tagged = Fable.Core.Util.jsNative

[<Fable.Core.Emit("String.raw"); TaggedTemplate>]
let raw (template: FormattableString) : string = Fable.Core.Util.jsNative

let greet (name: string) (count: int) = tag $"Hello {name}, {count} items"

let ofValue (template: FormattableString) = tag template

let mutable private evaluated = 0

let private makeTemplate () : FormattableString =
    evaluated <- evaluated + 1
    $"<b>{evaluated}</b>"
#endif

let tests =
    testList "TaggedTemplates" [
#if FABLE_COMPILER
        testCase "the tag receives the strings and the values" <| fun () ->
            let result = greet "Ada" 3
            List.ofSeq result.strings |> equal [ "Hello "; ", "; " items" ]
            result.values |> equal [| box "Ada"; box 3 |]

        testCase "the strings array is the same object on every call" <| fun () ->
            let first = greet "Ada" 1
            let second = greet "Grace" 2
            obj.ReferenceEquals(first.strings, second.strings) |> equal true

        testCase "values are passed unformatted" <| fun () ->
            let value = 1.5
            let result = tag $"{value}"
            result.values |> equal [| box 1.5 |]

        testCase "an emitted tag such as String.raw works" <| fun () ->
            let name = "Ada"
            raw $"Hi {name}!" |> equal "Hi Ada!"

        testCase "a FormattableString value is passed as its strings and arguments" <| fun () ->
            let result = ofValue $"a{1}b{2}c"
            List.ofSeq result.strings |> equal [ "a"; "b"; "c" ]
            result.values |> equal [| box 1; box 2 |]

        testCase "a FormattableString expression is evaluated once" <| fun () ->
            evaluated <- 0
            let result = tag (makeTemplate ())
            evaluated |> equal 1
            result.values |> equal [| box 1 |]
#endif
    ]

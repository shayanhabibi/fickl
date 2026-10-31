module Fable.Tests.ErasedTuples

open Util.Testing
open Fable.Core

type Init = { Method: string }

[<Erase>]
type Entry = Entry of key: string * value: int

[<Erase>]
type FetchArgs = FetchArgs of url: string * init: Init option

[<Erase>]
type Range = Range of start: int * stop: int option * step: int option

let url (FetchArgs(url, _)) = url
let init (FetchArgs(_, init)) = init

let length (value: obj) : int =
#if FABLE_COMPILER
    JsInterop.(?) value "length"
#else
    0
#endif

let tests =
    testList "ErasedTuples" [
        testCase "labelled fields read back" <| fun () ->
            let (Entry(key, value)) = Entry("a", 1)
            key |> equal "a"
            value |> equal 1

        testCase "optional fields read back" <| fun () ->
            let post = Some { Method = "POST" }
            init (FetchArgs("u", post)) |> equal post
            init (FetchArgs("u", None)) |> equal None
            url (FetchArgs("u", None)) |> equal "u"

        testCase "values with and without the trailing None are equal" <| fun () ->
            let none: Init option = List.tryHead []
            FetchArgs("u", None) |> equal (FetchArgs("u", none))
            Range(0, None, None) |> equal (Range(0, List.tryHead [], None))

        testCase "a None before a Some stays in place" <| fun () ->
            let (Range(start, stop, step)) = Range(1, None, Some 2)
            (start, stop, step) |> equal (1, None, Some 2)

#if FABLE_COMPILER
        testCase "trailing None values are left out of the tuple" <| fun () ->
            let none: int option = List.tryHead []
            length (FetchArgs("u", None)) |> equal 1
            length (Range(0, Some 5, None)) |> equal 2
            length (Range(0, None, none)) |> equal 1
            length (Range(0, Some 1, Some 2)) |> equal 3
            length (Entry("a", 1)) |> equal 2

        testCase "a tuple JavaScript made without its optional elements reads them as None" <| fun () ->
            let args: FetchArgs = unbox [| box "v" |]
            url args |> equal "v"
            init args |> equal None
#endif
    ]

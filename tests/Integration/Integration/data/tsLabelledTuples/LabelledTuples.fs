module LabelledTuples

open Fable.Core

type Init = abstract method: string

[<Erase>]
type Entry = Entry of key: string * value: int

[<Erase>]
type FetchArgs = FetchArgs of url: string * init: Init option

[<Erase>]
type Span = Span of start: int * stop: int option * step: int option

[<Erase>]
type Pair = Pair of string * int

[<Erase>]
type Keywords = Keywords of ``default``: string * ``my value``: int

let entry = Entry("a", 1)
let getArgs = FetchArgs("/", None)
let fetchArgs (init: Init option) = FetchArgs("/", init)
let range (stop: int option) = Span(0, stop, None)
let stepped = Span(0, None, Some 2)
let pair = Pair("a", 1)
let keywords = Keywords("a", 1)

let url (FetchArgs(url, _)) = url
let stop (Span(_, stop, _)) = stop

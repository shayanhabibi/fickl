module Spread

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core.JsInterop

type Options =
    abstract size: int with get, set
    abstract name: string with get, set
    abstract ``data-id``: string with get, set
    abstract onChange: (int -> int -> unit) with get, set
    abstract limit: int option with get, set

let resize (options: Options) = jsSpread options (fun o -> o.size <- options.size + 1)

let update (make: unit -> Options) =
    jsSpread (make ()) (fun o ->
        o.name <- "updated"
        o.``data-id`` <- "id"
        o.onChange <- fun previous next -> ignore (previous + next)
        o.limit <- None)

let resizeAll (all: Options list) = all |> List.map (fun options -> jsSpread options (fun o -> o.size <- 0))

let conditional (options: Options) (grow: bool) =
    jsSpread options (fun o ->
        if grow then
            o.size <- options.size * 2)

let merge (defaults: Options) (options: Options) = jsMerge [ defaults; options ]

let mergeWithLiteral (defaults: Options) = jsMerge [| defaults; jsOptions (fun o -> o.size <- 1) |]

let mergeAll (all: Options list) = jsMerge all

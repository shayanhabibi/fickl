module Records

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

type Config = { Port: int; Host: string }

/// A user interface with an indexer is an index signature
type Headers =
    [<EmitIndexer>]
    abstract Item: string -> string with get, set

type Scores = Record<string, int>

let scores () : Scores =
    let r = Record.empty ()
    r.["alice"] <- 3
    r

let score (name: string) (r: Scores) = r.[name]
let tryScore (name: string) (r: Scores) = Record.tryGet name r
let names (r: Scores) = Record.keys r
let frozen (r: ReadonlyRecord<string, int>) (name: string) = r.[name]

let labels () : PropertyRecord<Config, string> =
    let r: PropertyRecord<Config, string> = !!(createObj [ "Port", box "port"; "Host", box "host" ])
    r.[KeyOf.widen (keyof (fun (c: Config) -> c.Port))] <- "the port"
    r

let header (h: Headers) = h.["content-type"]

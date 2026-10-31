// Change: `Record<'K, 'V>`, `ReadonlyRecord` and `PropertyRecord`
// Fickl: `Fickl.Core.Record<'K, 'V>` is a plain object typed `Record<K, V>` in TypeScript, with `ReadonlyRecord` and `PropertyRecord` for `Readonly<Record<K, V>>` and `Record<keyof T, V>`, and a user `[<EmitIndexer>]` interface is declared with an index signature.
// Issues: 175, 142
// Args: --declaration
module Main

open Fable.Core
open Fickl.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

type Headers =
    [<EmitIndexer>]
    abstract Item: string -> string with get, set

let counts (words: string[]) : Record<string, int> =
    let counts = Record.empty ()

    for word in words do
        counts.[word] <-
            match Record.tryGet word counts with
            | Some n -> n + 1
            | None -> 1

    counts

let total (counts: ReadonlyRecord<string, int>) (word: string) = counts.[word]

let result = counts [| "a"; "b"; "a"; "constructor" |]

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal 2 result.["a"]
Check.equal 1 result.["constructor"]
Check.equal 2 (total (unbox result) "a")
Check.equal [| "a"; "b"; "constructor" |] (Record.keys result)
Check.equal true (declaration.Contains "export declare function counts(words: MutableArray<string>): globalThis.Record<string, int32>")
Check.equal true (declaration.Contains "[key: string]: string")

// Change: `--declaration`
// Fickl: Writes a TypeScript declaration file next to each JavaScript file, so TypeScript can call the compiled F#.
// Issues: 20, 21
// Args: --declaration
module Main

open Fable.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

let add (a: int) (b: int) = a + b

Check.equal true ((readFileSync (besideThisFile "Main.d.ts", "utf8")).Contains "export declare function add(a: int32, b: int32): int32")

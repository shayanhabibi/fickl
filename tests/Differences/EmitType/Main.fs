// Change: `[<EmitType>]`
// Fickl: Writes the TypeScript annotation of a type as the given text, for conditional, mapped and template literal types F# cannot describe; the JavaScript is unchanged.
// Issues: 145
// Args: --declaration
module Main

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

[<EmitType("`data-${string}`"); Erase>]
type DataAttr = DataAttr of string

[<EmitType("Record<string, $0>")>]
type Dict<'T> = interface end

[<EmitType("EventEmitter", "node:events")>]
type Emitter = interface end

let dataAttr (name: string) = DataAttr("data-" + name)
let readAttr (DataAttr value) = value
let counts (n: int) : Dict<int> = !!(createObj [ "n" ==> n ])
let emitter (e: Emitter) = e

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal "data-x" (readAttr (dataAttr "x"))
Check.equal true (declaration.Contains "export declare function dataAttr(name: string): `data-${string}`")
Check.equal true (declaration.Contains "export declare function counts(n: int32): Record<string, int32>")
Check.equal true (declaration.Contains "import type { EventEmitter } from \"node:events\"")
Check.equal true (declaration.Contains "export declare function emitter(e: EventEmitter): EventEmitter")

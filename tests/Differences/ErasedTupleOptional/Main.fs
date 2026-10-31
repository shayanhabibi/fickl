// Change: option fields ending an erased tuple
// Fickl: Trailing option fields of an erased single-case union are optional tuple elements, so `FetchArgs("/", None)` is `["/"]`, and named fields label the elements in TypeScript.
// Issues: 153
// Args: --declaration
module Main

open Fable.Core
open Fable.Core.JsInterop

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

[<Erase>]
type FetchArgs = FetchArgs of url: string * method: string option

let none: string option = List.tryHead []
let get = FetchArgs("/", None)
let alsoGet = FetchArgs("/", none)

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal 1 (get?length)
Check.equal 1 (alsoGet?length)
Check.equal true (declaration.Contains "export type FetchArgs = [url: string, method?: Option<string>]")

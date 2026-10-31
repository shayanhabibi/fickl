// Change: `unknown`, an abbreviation of `obj`
// Fickl: A module function or abstract member taking or returning `Fickl.Core.unknown` has `unknown` in its TypeScript signature, so TypeScript callers must narrow the value; `obj` stays `any`.
// Issues: 150
// Args: --declaration
module Main

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

let parse (text: string) : unknown = JS.JSON.parse text
let field (value: unknown) (name: string) : unknown = value?(name)
let anything (value: obj) : obj = value

type IStore =
    abstract Get: key: string -> unknown

let store = { new IStore with member _.Get key = box key.Length }

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal 1 (unbox<int> (field (parse "{\"a\":1}") "a"))
Check.equal 3 (unbox<int> (store.Get "abc"))
Check.equal true (declaration.Contains "export declare function parse(text: string): unknown")
Check.equal true (declaration.Contains "export declare function field(value: unknown, name: string): unknown")
Check.equal true (declaration.Contains "export declare function anything(value: any): any")
Check.equal true (declaration.Contains "Get(key: string): unknown")

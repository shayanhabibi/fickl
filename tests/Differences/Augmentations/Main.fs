// Change: `[<AugmentGlobal>]` and `[<AugmentModule>]`
// Fickl: An interface marked `[<AugmentGlobal>]` or `[<AugmentModule>]` adds its members to a global or module interface in TypeScript, `declare global { interface HTMLElementTagNameMap { ... } }`, so TypeScript code sees what an F# library contributes.
// Issues: 154
// Args: --declaration
module Main

open Fable.Core
open Fickl.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

type Counter =
    abstract count: int

[<AugmentGlobal("HTMLElementTagNameMap")>]
type TagNames =
    [<EmitProperty("my-counter")>]
    abstract counter: Counter

[<AugmentModule("vue", "ComponentCustomProperties")>]
type Translations =
    abstract translate: key: string -> string

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal true (declaration.Contains "declare global {")
Check.equal true (declaration.Contains "interface HTMLElementTagNameMap {")
Check.equal true (declaration.Contains "get \"my-counter\"(): Counter")
Check.equal true (declaration.Contains "declare module \"vue\" {")
Check.equal true (declaration.Contains "translate(key: string): string")

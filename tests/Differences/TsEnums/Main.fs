// Change: `[<TsEnum>]` and imported `[<StringEnum>]` unions
// Fickl: Emits a `[<TsEnum>]` enum as the object a TypeScript enum compiles to, typed by an ambient enum in TypeScript, and compiles the cases of an imported or global `[<StringEnum>]` union to members of the imported enum object.
// Issues: 149
module Main

open Fable.Core
open Fickl.Core

[<TsEnum>]
type Color =
    | Red = 0
    | Green = 1

// The imported object's values differ from the CompiledNames, so reading them shows where a case's value comes from
[<Global("Dir"); StringEnum>]
type Dir =
    | [<CompiledName("UP")>] Up
    | [<CompiledName("DOWN")>] Down

JsInterop.emitJsStatement () "globalThis.Dir = Object.freeze({ Up: 'up-member', Down: 'down-member' })"

// JavaScript can read the enum's members, and its reverse mapping
Check.equal 1 (JsInterop.emitJsExpr () "Color.Green")
Check.equal "Green" (JsInterop.emitJsExpr () "Color[1]")

// An imported string enum case is the member of the enum object
Check.equal "up-member" (string Dir.Up)
Check.equal "down" (match JsInterop.emitJsExpr () "'down-member'" with Dir.Down -> "down" | Dir.Up -> "up")

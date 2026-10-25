// Change: `[<Curried>]` on a record field
// Fickl: Keeps a function stored in a record field curried, for a JS object that stores or calls it one argument at a time.
// Issues: 41
module Main

open Fable.Core.JsInterop
open Fickl.Core

type Handlers =
    {
        [<Curried>]
        onKey: string -> int -> string
    }

let fromJs: Handlers = emitJsExpr () "({ onKey: key => code => key + code })"
Check.equal "a1" (fromJs.onKey "a" 1)

let fromFSharp = { onKey = fun key code -> key + string code }
Check.equal "b2" (emitJsExpr fromFSharp "$0.onKey('b')(2)")

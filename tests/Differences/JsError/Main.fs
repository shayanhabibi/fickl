// Change: `[<JsError>]`
// Fickl: An F# exception marked `[<JsError>]` is a JS `Error` subclass with a `stack`, and still matches and compares as an F# exception.
// Issues: 157
module Main

open Fable.Core.JsInterop

[<Fickl.Core.JsError>]
exception Boom of code: int

exception Plain of code: int

let isError (e: exn) : bool = emitJsExpr e "$0 instanceof Error"

let caught =
    try
        raise (Boom 3)
    with
    | Failure _ -> None
    | Boom code -> Some code

Check.equal (Some 3) caught
Check.equal true (isError (Boom 3))
Check.equal "string" (emitJsExpr (Boom 3) "typeof $0.stack")
Check.equal false (isError (Plain 3))
Check.equal true (Boom 3 = Boom 3)

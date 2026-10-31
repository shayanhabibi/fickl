// Change: `--nativeErrors`
// Fickl: Every exception, from `failwith` to a declared F# exception, is a JS `Error` with a `stack`, and an inner exception is its `cause`.
// Issues: 157
// Args: --nativeErrors
module Main

open Fable.Core.JsInterop

exception Boom of code: int

let isError (e: exn) : bool = emitJsExpr e "$0 instanceof Error"
let stackType (e: exn) : string = emitJsExpr e "typeof $0.stack"

let caught =
    try
        failwith "boom"
    with e ->
        e

Check.equal true (isError caught)
Check.equal "string" (stackType caught)
Check.equal "boom" caught.Message
Check.equal true (isError (Boom 3))
Check.equal "string" (stackType (Boom 3))
Check.equal true (isError (System.ArgumentException "bad"))
Check.equal true (Boom 3 = Boom 3)

let inner = exn "inner"
let outer = System.Exception("outer", inner)
Check.equal true (emitJsExpr (outer, inner) "$0.cause === $1")
Check.equal "inner" outer.InnerException.Message

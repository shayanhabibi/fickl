// Change: catching values JavaScript throws
// Fickl: `:? JS.TypeError` and the other `Fickl.Core.JS` error bindings catch JS errors by type, `JS.Thrown` matches a thrown value that is not an error, and `e.Message` of a thrown string is the string.
// Issues: 158
module Main

open Fable.Core.JsInterop
open Fickl.Core

let throwJs (statement: string) : unit = emitJsStatement statement "eval($0)"

let classify (statement: string) =
    try
        throwJs statement
        "none"
    with
    | :? JS.TypeError as e -> "TypeError: " + e.message
    | :? JS.DOMException as e when e.name = "AbortError" -> "aborted"
    | :? JS.Error as e -> e.name
    | JS.Thrown value -> "thrown " + string value
    | e -> "exn: " + e.Message

let message (statement: string) =
    try
        throwJs statement
        "none"
    with e ->
        e.Message

Check.equal "TypeError: bad" (classify "throw new TypeError('bad')")
Check.equal "RangeError" (classify "throw new RangeError('far')")
Check.equal "aborted" (classify "throw new DOMException('stop', 'AbortError')")
Check.equal "thrown boom" (classify "throw 'boom'")
Check.equal "boom" (message "throw 'boom'")
Check.equal "undefined" (message "throw undefined")

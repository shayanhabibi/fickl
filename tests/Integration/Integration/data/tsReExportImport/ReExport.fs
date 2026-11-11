module ReExport

open Fable.Core.JsInterop

type Counter =
    abstract value: int

// TypeScript keeps the constant: its annotation asserts the F# type, which a re-export would
// replace with whatever the module declares
let counter: Counter = importMember "./lib.js"
let anything: obj = import "counter" "./lib.js"
let count: int = importMember "./lib.js"

let readCount () = count + counter.value

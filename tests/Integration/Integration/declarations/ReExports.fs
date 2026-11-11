module ReExports

open Fable.Core.JsInterop

type Counter =
    abstract value: int

// Re-exported imports are declared with their F# types
let counter: Counter = importMember "./lib.js"
let renamed: int = import "count" "./lib.js"
let everything: obj = importAll "./lib.js"
let bump: unit -> unit = importMember "./lib.js"

let bumpTwice () =
    bump ()
    bump ()

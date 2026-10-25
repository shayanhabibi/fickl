// Change: `[<Curried>]` on a parameter
// Fickl: Keeps a function parameter curried, for JavaScript that calls it one argument at a time.
// Issues: 24
module Main

open Fable.Core
open Fickl.Core

[<Emit("$0(1)(2)")>]
let callOneAtATime ([<Curried>] f: int -> int -> int) : int = jsNative

Check.equal 3 (callOneAtATime (fun a b -> a + b))

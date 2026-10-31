// Change: members of a class inheriting a JavaScript class
// Fickl: A class whose base is a global or imported JavaScript class attaches its members, so JavaScript sees `static observedAttributes` and its methods.
// Issues: 162
module Main

open Fable.Core
open Fable.Core.JsInterop

[<Global>]
type EventTarget() =
    class
    end

type Counter() =
    inherit EventTarget()
    static member observedAttributes = [| "count" |]
    member _.Count = 1

let observed: string[] = jsConstructor<Counter>?observedAttributes
Check.equal "count" observed.[0]
Check.equal 1 (Counter()?Count)

// Change: `let` fields of a class inheriting a JavaScript class
// Fickl: The fields are ES `#private` fields, so `let size` in a subclass of `Map` does not try to set `Map`'s read-only `size`.
// Issues: 161
module Main

open Fable.Core

[<Global("Map")>]
type JsMap() =
    class
    end

type Bag(count: int) =
    inherit JsMap()
    let size = count
    member _.Count = size

Check.equal 3 (Bag(3).Count)

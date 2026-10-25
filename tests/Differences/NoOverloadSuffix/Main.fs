// Change: `[<NoOverloadSuffix>]`
// Fickl: Drops the overload hash from a method's compiled name, `Counter__Add` instead of `Counter__Add_Z524259A4`, so JavaScript can call it by a predictable name. Overloaded members keep their hash.
// Issues: 35
module Main

open Fable.Core.JsInterop
open Fickl.Core

type Counter(start: int) =
    [<NoOverloadSuffix>]
    member _.Add(x: int) = start + x

Check.equal 11 (emitJsExpr (Counter(10), 1) "Counter__Add($0, $1)")
Check.equal 11 (Counter(10).Add 1)

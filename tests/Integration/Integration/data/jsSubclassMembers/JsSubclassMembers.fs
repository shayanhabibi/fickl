module JsSubclassMembers

open Fable.Core

[<Global>]
type EventTarget() =
    abstract dispatchEvent: obj -> bool
    default _.dispatchEvent(_: obj) = jsNative

type Counter(start: int) =
    inherit EventTarget()
    let mutable count = start
    static member observedAttributes = [| "count" |]
    member _.Count = count
    member _.Increment() = count <- count + 1
    override _.dispatchEvent(_: obj) = true

type DoubleCounter() =
    inherit Counter(10)
    member this.IncrementTwice() =
        this.Increment()
        this.Increment()

[<AttachMembers(false)>]
type DetachedCounter() =
    inherit EventTarget()
    member _.Value = 5

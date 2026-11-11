module AttachedExtensions

open Fable.Core

[<AttachMembers>]
type Counter(count: int) =
    member _.Count = count

// An intrinsic extension is part of the class
type Counter with
    member this.Next = Counter(this.Count + 1)

[<AttachMembers>]
type Box<'T>(item: 'T) =
    member val Item = item with get, set

// An optional extension in the file of an [<AttachMembers>] class is compiled into the class
[<AutoOpen>]
module Extensions =
    type Counter with
        member this.Double = this.Count * 2
        member this.Add(n: int) = Counter(this.Count + n)
        static member Zero = Counter(0)
        static member Of(n: int) = Counter(n)

    type Box<'T> with
        member this.Map(f: 'T -> 'U) = Box(f this.Item)

        member this.Content
            with get () = this.Item
            and set (v: 'T) = this.Item <- v

    // Not an [<AttachMembers>] class: a module function
    type System.String with
        member this.Twice = this + this

let total = (Counter.Of 2).Add(3).Double + Counter.Zero.Next.Count
let content = Box(1).Map(fun x -> string x).Content.Twice

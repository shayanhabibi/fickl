// Change: explicit constructors in an `[<AttachMembers>]` class
// Fickl: Constructing an [<AttachMembers>] class through an explicit or secondary constructor runs that constructor.
// Issues: 334
module Main

open Fable.Core

[<AttachMembers>]
type AttachedExplicitOverloads =
    val A: int
    val B: string
    new(a: int) = { A = a; B = "b" }
    new(a: int, b: string) = { A = a; B = b }
    member x.Show() = string x.A + x.B

[<AttachMembers>]
type AttachedSecondary(a: int, b: string) =
    new(a: int) = AttachedSecondary(a + 1, "s")
    member _.Show() = string a + b

Check.equal "1b" (AttachedExplicitOverloads(1).Show())
Check.equal "2z" (AttachedExplicitOverloads(2, "z").Show())
Check.equal "2s" (AttachedSecondary(1).Show())

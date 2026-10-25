// Change: `[<ReferenceEquality>]` and `[<NoEquality>]` records and unions
// Fickl: Compares them by reference, as .NET does; two values with equal fields are not equal.
// Issues: 30
module Main

open System.Collections.Generic

[<ReferenceEquality>]
type Node = { id: int }

[<NoEquality; NoComparison>]
type Handle = Handle of int

let a = { id = 1 }
let b = { id = 1 }

Check.equal false (a = b)
Check.equal true (a = a)
Check.equal false ([ a ] = [ b ])
Check.equal 2 (HashSet<Node>([ a; b ]).Count)
Check.equal false ((box (Handle 1)).Equals(box (Handle 1)))

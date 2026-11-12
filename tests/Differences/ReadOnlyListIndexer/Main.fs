// Change: indexing an `IReadOnlyList<'T>`
// Fickl: `xs.[i]` on an `IReadOnlyList` indexes an array, a ResizeArray or an F# list instead of calling a .NET member name that arrays do not have.
// Issues: 431
module Main

open System.Collections.Generic

let first (xs: IReadOnlyList<string>) = xs.[0]
let last (xs: IReadOnlyList<string>) = xs.[xs.Count - 1]

Check.equal "a" (first [| "a"; "b" |])
Check.equal "d" (last (ResizeArray [ "c"; "d" ]))
Check.equal "f" (last [ "e"; "f" ])

// A library may declare its own types in Fable.Core.JS so `open Fable.Core.JS` brings them into scope.
// Only Fable.Core's own bindings there are JavaScript globals.
namespace Fable.Core.JS

module FicklTestLibrary =
    let inline double (x: int) = x * 2

    type Counter(start: int) =
        let mutable count = start
        member _.Next() =
            count <- count + 1
            count

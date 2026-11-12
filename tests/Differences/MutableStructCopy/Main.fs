// Change: copying a `mutable` struct record
// Fickl: `{ m with ... }` and a byref to a mutable struct record read the struct's fields, not those of a reference cell.
// Issues: 254
module Main

[<Struct>]
type S = { SA: int; SB: int }

let mutable moduleStruct = { SA = 1; SB = 2 }

let copyModule () =
    moduleStruct <- { moduleStruct with SB = 6 }
    { moduleStruct with SA = 7 }

let copyLocal () =
    let mutable local = { SA = 1; SB = 2 }
    local <- { local with SB = 5 }
    { local with SA = 4 }

let readByref () =
    let mutable local = { SA = 1; SB = 2 }
    let address = &local
    address <- { SA = 8; SB = 9 }
    address.SA

Check.equal { SA = 7; SB = 6 } (copyModule ())
Check.equal { SA = 4; SB = 5 } (copyLocal ())
Check.equal 8 (readByref ())

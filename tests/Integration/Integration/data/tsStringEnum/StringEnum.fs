module StringEnum

open Fable.Core

// A string enum keeps its declared type in TypeScript, in a signature and as a generic argument.
// The other snapshot cases compile for JavaScript in the same process, where it is typed as a string (#398)
[<StringEnum>]
type Level =
    | Low
    | High

let describe (level: Level) : string =
    match level with
    | Low -> "low"
    | High -> "high"

let levelNames (levels: Level seq) : Level seq = levels |> Seq.filter (fun level -> level = High)

let pair (level: Level) : Level * Level list = level, [ level ]

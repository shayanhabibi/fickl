// Change: `--strictOptions`
// Fickl: `Some null` of a string, array, class, record or function stays `Some`, as on .NET, built the same way at concrete and generic sites. Value options stay erased.
// Issues: 32
// Args: --strictOptions
module Main

[<AllowNullLiteral>]
type Box() =
    member _.Size = 1

type Point = { x: int }

let wrap (x: 'T) = Some x

let read (o: string option) =
    match o with
    | Some v -> if isNull v then "null" else v
    | None -> "none"

let s: string = null

Check.equal true (Some s).IsSome
Check.equal true (Some s = wrap s)
Check.equal "null" (read (Some s))
Check.equal "text" (read (Some "text"))
Check.equal "none" (read None)
Check.equal true (Some(null: int[])).IsSome
Check.equal true (Some(null: Box)).IsSome
Check.equal true (Some(Unchecked.defaultof<Point>)).IsSome
Check.equal true (Some(Unchecked.defaultof<int -> int>)).IsSome
Check.equal true (Some(System.Nullable<int>())).IsSome
Check.equal false (Option.ofObj s).IsSome

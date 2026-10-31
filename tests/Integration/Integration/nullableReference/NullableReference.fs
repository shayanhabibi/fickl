module NullableReference

let orDefault (s: string | null) : string =
    match s with
    | null -> "default"
    | s -> s

let fromNull = orDefault null

let tryFirst (xs: string array) : string | null =
    if xs.Length = 0 then null else xs[0]

let names: (string | null) array = [| "a"; null |]

let findOrNull (index: int) (list: 'T list) : 'T | null when 'T: not struct =
    match List.tryItem index list with
    | Some item -> item
    | None -> null

let nullableInt (x: System.Nullable<int>) = x

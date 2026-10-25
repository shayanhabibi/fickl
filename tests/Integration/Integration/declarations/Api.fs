module Api

open Shapes

let origin = { X = 0.0; Y = 0.0 }

let mutable counter = 0

let describe (color: Color) (value: NumberOrText) = $"{color} {value}"

let scale (factor: float) (shapes: Shape list) =
    shapes |> List.map (function
        | Circle r -> Circle(r * factor)
        | Rect(w, h) -> Rect(w * factor, h * factor)
        | Empty -> Empty)

let firstOr (fallback: 'T) (items: 'T list) =
    match items with
    | x :: _ -> x
    | [] -> fallback

type Greeter(name: string, ?greeting: string) =
    member _.Greet() = (defaultArg greeting "Hello") + ", " + name
    member _.Greet(times: int) = String.replicate times name

let makeAdder = fun (a: int) -> fun (b: int) -> a + b

let nameOf (named: Contracts.INamed) = named.Name

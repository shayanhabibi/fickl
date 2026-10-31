module Readonly

open System.Collections.Generic
open Fickl.Core

type Point = { x: float; y: float }

let count (xs: IReadOnlyCollection<int>) = xs.Count

let total (xs: IReadOnlyList<float>) = Seq.sum xs

let lookup (d: IReadOnlyDictionary<string, int>) (key: string) =
    match d.TryGetValue key with
    | true, value -> value
    | _ -> d.Count

let draw (points: JS.ReadonlyArray<Point>) =
    let mutable length = 0.
    for i in 1 .. points.length - 1 do
        length <- length + abs (points.[i].x - points.[i - 1].x)
    length

let names (xs: JS.ReadonlyArray<string>) = xs.join ", "

let callers () =
    count (HashSet [ 1; 2 ])
    + count [| 1 |]
    + int (total [ 1.; 2. ])
    + lookup (Map [ ("a", 1) ]) "a"
    + lookup (Dictionary()) "b"
    + int (draw (JS.ReadonlyArray.ofArray [| { x = 0.; y = 0. }; { x = 2.; y = 0. } |]))
    + (names (JS.ReadonlyArray.ofResizeArray (ResizeArray [ "a" ]))).Length

let copy (xs: JS.ReadonlyArray<int>) = JS.ReadonlyArray.toArray xs

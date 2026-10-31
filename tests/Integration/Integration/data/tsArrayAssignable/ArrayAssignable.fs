module ArrayAssignable

open Fable.Core

// An F# array of anything but numbers is a plain JS array, so its `MutableArray<T>` type is
// assignable to a TS `T[]`
type Point = { x: float; y: float }

[<Import("countPoints", "./points.ts")>]
let countPoints (points: Point[]) : int = jsNative

[<Import("joinAll", "./points.ts")>]
let joinAll (parts: string[]) : string = jsNative

[<Import("sum", "./points.ts")>]
let sum (values: float[]) : float = jsNative

[<Import("Holder", "./points.ts")>]
type Holder() =
    member val items: string[] = jsNative with get, set

let countAll (pts: Point[]) = countPoints pts

let countMapped (xs: float[]) =
    xs |> Array.map (fun x -> { x = x; y = x }) |> countPoints

let countFiltered (pts: Point[]) =
    pts |> Array.filter (fun p -> p.x > 0.) |> countPoints

let joinPair (pair: string[] * int) = joinAll (fst pair)

let setItems (holder: Holder) (items: string list) = holder.items <- List.toArray items

let identity (xs: 'T[]) = xs

let countGeneric (pts: Point[]) = countPoints (identity pts)

// A float array may be a Float64Array, so it is passed only where an array-like is accepted
let total (xs: float[]) = sum xs

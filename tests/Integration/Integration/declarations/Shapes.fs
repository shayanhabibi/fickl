module Shapes

open Fable.Core

/// A plain union
type Shape =
    | Circle of radius: float
    | Rect of width: float * height: float
    | Empty

[<StringEnum>]
type Color =
    | Red
    | [<CompiledName("dark-blue")>] DarkBlue

[<Erase>]
type NumberOrText =
    | Number of float
    | Text of string

type Point = { X: float; Y: float }

type IArea =
    abstract Area: unit -> float

type Square(side: float) =
    member _.Side = side
    member val Name = "square" with get, set
    static member Unit = Square(1.0)
    interface IArea with
        member _.Area() = side * side

type private Hidden = { Secret: int }

let private hidden = { Secret = 1 }

let area shape =
    match shape with
    | Circle r -> System.Math.PI * r * r
    | Rect(w, h) -> w * h
    | Empty -> 0.0

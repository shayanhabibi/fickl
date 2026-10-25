module Lib

open System.Runtime.InteropServices
open Fable.Core

[<Literal>]
let Greeting = "hi"

let inline double x = x * 2
let inline inc x = x + 1
let helper (s: string) = s + "!"
let inline shout (s: string) = helper (s.ToUpper())

let plain x = x - 1
let mutable counter = 0

type Shape =
    | Circle of float
    | Square of float

[<StringEnum>]
type Color =
    | Red
    | [<CompiledName("dark-blue")>] DarkBlue

type Point = { X: int; Y: int }

type Calc() =
    static member Add(a: int, [<Optional; DefaultParameterValue(1)>] b: int) = a + b

[<Emit("($0 + 100)")>]
let addHundred (x: int) : int = jsNative

let inline add (a: ^T) (b: ^T) : ^T = a + b

let ident x = x

type IShape =
    abstract Area: float
    abstract Name: string

type Counter() =
    member val Count = 0 with get, set
    member inline this.Bump() = this.Count <- this.Count + 1
    member this.Reset() = this.Count <- 0

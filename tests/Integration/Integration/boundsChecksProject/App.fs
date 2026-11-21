module App

open Fickl.Core

let read (a: float[]) i = a.[i + 1]
let write (a: float[]) i v = a.[i + 1] <- v
let stringRead (s: string) i = s.[i + 1]
let grid (g: float[,]) i j = Array2D.set g i (j + 1) g.[i, j]
let resize (r: ResizeArray<int>) i = r.[i + 1] <- r.[i]

let inline inlineRead (a: float[]) i = a.[i + 1]
let callsOwnInline (a: float[]) i = inlineRead a i
let callsReferencedInline (a: float[]) i = Referenced.inlineRead a i

[<BoundsChecks(true)>]
let checkedAgain (a: float[]) i = a.[i + 1]

[<BoundsChecks(true)>]
module Checked =
    let read (a: float[]) i = a.[i + 1]
    let callsOwnInline (a: float[]) i = inlineRead a i

type Solver(data: float[]) =
    member _.At i = data.[i + 1]

let table = Array.zeroCreate<float> 4

for i in 0..2 do
    table.[i + 1] <- float i

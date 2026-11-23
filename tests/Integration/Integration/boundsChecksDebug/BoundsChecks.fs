module BoundsChecks

open Fickl.Core

[<BoundsChecks(false)>]
module Marked =
    let read (a: float[]) i = a.[i + 1]
    let readLocal (a: float[]) i = a.[i]
    let write (a: float[]) i v = a.[i + 1] <- v
    let writeLocal (a: float[]) i v = a.[i] <- v
    let getSet (a: int[]) i = Array.set a (i * 2) (Array.get a i)
    let item (a: int[]) i = Array.item (i - 1) a
    let grid (g: float[,]) i j = Array2D.set g i (j + 1) g.[i, j]
    let resize (r: ResizeArray<int>) i = r.[i + 1] <- r.[i]
    let stringRead (s: string) i = s.[i + 1]
    let stringChars (s: string) i = s.Chars(i + 1)
    let iList (xs: System.Collections.Generic.IList<float>) i = xs.[i + 1] <- xs.[i]
    let lambda (a: float[]) = Array.init 3 (fun i -> a.[i + 1])

    let comparer (a: int[]) =
        { new System.Collections.Generic.IComparer<int> with
            member _.Compare(x, y) = a.[x + 1] - a.[y + 1]
        }

    let inline inlineRead (a: float[]) i = a.[i + 1]

    [<BoundsChecks(true)>]
    let checkedAgain (a: float[]) i = a.[i + 1]

    type Solver(data: float[]) =
        member _.At i = data.[i + 1]

[<BoundsChecks(false)>]
module Init =
    let table = Array.zeroCreate<float> 4

    for i in 0..2 do
        table.[i + 1] <- float i

module Plain =
    let read (a: float[]) i = a.[i + 1]
    let stringRead (s: string) i = s.[i + 1]
    let iList (xs: System.Collections.Generic.IList<float>) i = xs.[i + 1] <- xs.[i]
    let inline inlineRead (a: float[]) i = a.[i + 1]
    let callsMarkedInline (a: float[]) i = Marked.inlineRead a i

    [<BoundsChecks(false)>]
    let first = Init.table.[1 + 0]

[<BoundsChecks(false)>]
module MarkedCaller =
    let callsPlainInline (a: float[]) i = Plain.inlineRead a i

[<BoundsChecks(false)>]
type Grid(cells: float[]) =
    member _.Fast i = cells.[i * 2]

    [<BoundsChecks(true)>]
    member _.Safe i = cells.[i * 2]

[<BoundsChecks(false)>]
type Buffer(n: int) =
    let data = Array.zeroCreate<float> n
    do data.[n - 1] <- 1.0
    let twice = data.[n - 1] * 2.0
    member _.Last = data.[n - 1]
    member _.Twice = twice

type Mixed(d: float[]) =
    [<BoundsChecks(false)>]
    member _.Fast i = d.[i + 1]

    member _.Safe i = d.[i + 1]

    [<BoundsChecks(false)>]
    member _.Second = d.[1]

[<BoundsChecks(false)>]
module MarkedLoops =
    let inline forRange n ([<InlineIfLambda>] body: int -> unit) =
        for i in 0 .. n - 1 do
            body i

module PlainLoops =
    let inline forRange n ([<InlineIfLambda>] body: int -> unit) =
        for i in 0 .. n - 1 do
            body i

module PlainApp =
    let data = Array.zeroCreate<float> 4
    let bump i = data.[i + 1] <- data.[i] + 1.0
    let run () = MarkedLoops.forRange 3 bump

[<BoundsChecks(false)>]
module MarkedApp =
    let data = Array.zeroCreate<float> 4
    let bump i = data.[i + 1] <- data.[i] + 1.0
    let run () = PlainLoops.forRange 3 bump

module PlainWrap =
    let inline wrapMarked (a: float[]) i = Marked.inlineRead a i

[<BoundsChecks(false)>]
module MarkedWrap =
    let inline wrapPlain (a: float[]) i = Plain.inlineRead a i

module NestedCalls =
    let viaPlainWrap (a: float[]) i = PlainWrap.wrapMarked a i
    let viaMarkedWrap (a: float[]) i = MarkedWrap.wrapPlain a i

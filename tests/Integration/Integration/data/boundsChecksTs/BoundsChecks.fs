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

module Plain =
    let read (a: float[]) i = a.[i + 1]
    let stringRead (s: string) i = s.[i + 1]
    let iList (xs: System.Collections.Generic.IList<float>) i = xs.[i + 1] <- xs.[i]
    let inline inlineRead (a: float[]) i = a.[i + 1]
    let callsMarkedInline (a: float[]) i = Marked.inlineRead a i

// Change: operators of an `[<Erase>]` union
// Fickl: A custom operator of an erased union is used when an operand is built with the case constructor, not the native operator.
// Issues: 280
module Main

open Fable.Core

[<Erase>]
type Num =
    | Num of int

    static member (+)(Num x, Num y) = 2 * (x + y)

let addRight (x: Num) (y: int) = x + Num y
let addNew (x: int) (y: int) = Num x + Num y

Check.equal 6 (addRight (Num 1) 2)
Check.equal 6 (addNew 1 2)

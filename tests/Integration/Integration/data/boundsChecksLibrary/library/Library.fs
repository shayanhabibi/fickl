module BoundsLib

open Fickl.Core

[<BoundsChecks(false)>]
module Marked =
    let inline read (a: float[]) i = a.[i + 1]

module Plain =
    let inline read (a: float[]) i = a.[i + 1]

[<BoundsChecks(false)>]
module MarkedWrap =
    let inline wrapPlain (a: float[]) i = Plain.read a i

module PlainWrap =
    let inline wrapMarked (a: float[]) i = Marked.read a i

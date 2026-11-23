module App

open Fickl.Core

// Inline functions of a precompiled library keep the [<BoundsChecks>] scope they are written in
let plainCallsMarked (a: float[]) i = BoundsLib.Marked.read a i

[<BoundsChecks(false)>]
let markedCallsPlain (a: float[]) i = BoundsLib.Plain.read a i

let viaMarkedWrap (a: float[]) i = BoundsLib.MarkedWrap.wrapPlain a i

[<BoundsChecks(false)>]
let viaPlainWrap (a: float[]) i = BoundsLib.PlainWrap.wrapMarked a i

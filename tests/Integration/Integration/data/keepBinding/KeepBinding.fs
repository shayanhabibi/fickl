module KeepBinding

// Regression fixture: a release build replaces a binding that is used once by its value, and
// `keepBinding` keeps it. See `KeptBinding` in Fable.Transforms/FableTransforms.fs (#309).

open Fickl.Core.JsInterop

let useA () = true
let useCtx () = 1
let useScroll (threshold: int, scrollRef: int) = threshold > scrollRef

// The shape of #309: both calls move into the condition, in their original order
let inlined threshold =
    let smOrAbove = useA ()
    let scrollRef = useCtx ()
    let isThreshold = useScroll (threshold, scrollRef)
    isThreshold && smOrAbove

let kept threshold =
    let smOrAbove = useA ()
    let scrollRef = keepBinding (useCtx ())
    let isThreshold = keepBinding (useScroll (threshold, scrollRef))
    isThreshold && smOrAbove

// A value that is not a call, an unused binding and a function are kept too
let keptValues (x: int) =
    let y = keepBinding (x + 1)
    let unused = keepBinding (useCtx ())
    let add = keepBinding (fun a b -> a + b + y)
    add 1 2

// Outside the value of a binding it is the identity
let notABinding (x: int) = keepBinding (x + 1) + keepBinding 2

exception Failed of int * int

// A call is not moved after another call inside the thrown expression, nor after the throw
let throwArgs () : int =
    let first = useCtx ()
    raise (Failed(useCtx (), first))

let afterThrow () : int =
    let first = useCtx ()
    failwith "thrown" |> ignore
    first

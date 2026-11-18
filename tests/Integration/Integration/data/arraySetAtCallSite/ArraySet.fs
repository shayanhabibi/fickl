module ArraySet

// fickl: a write to a local array is stored where it is written, and the library's `setItem` runs only for an
// index out of range. Every array write of an app would otherwise share the store inside `setItem`, which V8
// cannot specialise once it has seen several kinds of array.

let setLocal (xs: float[]) (i: int) (v: float) = xs[i] <- v

let setConstant (xs: int[]) = xs[2] <- 7

let setFromCall (xs: string[]) (i: int) (next: unit -> string) = xs[i] <- next ()

let setByModule (xs: int[]) (i: int) = Array.set xs i 3

let setResizeArray (xs: ResizeArray<string>) (i: int) = xs[i] <- "a"

// An index that cannot be written twice keeps the call, so that it is evaluated once
let setAtCallIndex (xs: float[]) (next: unit -> int) = xs[next ()] <- 1.

// So does an array that is not a local
let setInCallResult (make: unit -> int[]) (i: int) = Array.set (make ()) i 3

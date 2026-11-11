module RangeStepLoop

// A range with a constant int step is a counted loop, with no range sequence or enumerator
let sumUp (n: int) =
    let mutable acc = 0

    for i in 0 .. 2 .. n do
        acc <- acc + i

    acc

let sumDown (n: int) =
    let mutable acc = 0

    for i in n .. -3 .. 0 do
        acc <- acc + i

    acc

// The upper bound is read once, before the loop
let everyOther (xs: ResizeArray<int>) =
    let found = ResizeArray<int>()

    for i in 0 .. 2 .. xs.Count - 1 do
        found.Add xs[i]

    found

// Each closure captures the value of its own iteration
let thunks () =
    let fns = ResizeArray<unit -> int>()

    for i in 1 .. 5 .. 20 do
        fns.Add(fun () -> i)

    fns

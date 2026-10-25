// Change: reading another file's `mutable` value
// Fickl: The read stays before later side effects; it is not inlined past a call that changes the value.
// Issues: 15
module Main

let callsDuring () =
    let before = Counter.calls
    Counter.track 1 |> ignore
    Counter.calls - before

Check.equal 1 (callsDuring ())

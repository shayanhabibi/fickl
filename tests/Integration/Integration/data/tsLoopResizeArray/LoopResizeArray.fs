module LoopResizeArray

// A ResizeArray used once inside a loop stays bound outside it, so it is one typed array
// rather than a new untyped `[]` per iteration
let collect (xs: int[]) =
    let found = ResizeArray<int>()

    for x in xs do
        found.Add x

let collectWhile (n: int) =
    let found = ResizeArray<string>()
    let mutable i = 0

    while i < n do
        found.Add(string i)
        i <- i + 1

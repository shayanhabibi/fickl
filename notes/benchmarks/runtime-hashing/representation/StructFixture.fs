module StructFixture

[<Struct>]
type Point =
    {
        X: int
        Y: int
    }

// Intentionally only numeric fields, local uses, no calls/boxing/escapes.
let localKernel iterations seed =
    let mutable checksum = seed

    for i in 0 .. iterations - 1 do
        let p =
            {
                X = checksum + i
                Y = checksum - i
            }

        let q = { p with X = p.Y + 3 }
        checksum <- (q.X ^^^ p.X) + q.Y

    checksum

[<Struct>]
type MutablePoint =
    {
        mutable X: int
        Cell: int ref
    }

type IValue =
    abstract Value: int

[<Struct>]
type Value(x: int) =
    member _.X = x

    interface IValue with
        member _.Value = x

let mutableCopy () =
    let cell = ref 5

    let mutable a =
        {
            X = 1
            Cell = cell
        }

    let mutable b = a
    b.X <- 9
    b.Cell.Value <- 7
    [| a.X; b.X; a.Cell.Value; b.Cell.Value |]

let boxPoint x =
    box
        {
            X = x
            Y = x + 1
        }

let interfaceValue x = (Value(x) :> IValue).Value

let closurePoint x =
    let p =
        {
            X = x
            Y = x + 1
        }

    fun () -> p

let observe () =
    let boxed = unbox<Point>(boxPoint 4)
    let closure = closurePoint 8

    Array.concat
        [|
            mutableCopy ()
            [| boxed.X; boxed.Y; interfaceValue 6; (closure ()).X; (closure ()).Y |]
        |]

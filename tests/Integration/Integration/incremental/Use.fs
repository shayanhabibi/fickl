module Use

open Lib

let values () =
    [|
        string (double 5)
        string (Mid.incTwice 1)
        shout "a"
        Greeting
        string (plain 3)
        string counter
        string (Calc.Add(1))
        string (addHundred 1)
        string (add 1 2)
        string (ident 7)
        string (Sig.triple 2)
        Sig.greet "x"
        Mid.describe { X = 1; Y = 2 }
        string (Other.unrelated ())
    |]

let area shape =
    match shape with
    | Circle r -> r * r
    | Square s -> s * s

let squareArea () = area (Square 2.0)

let colors = [| Red; DarkBlue |]

let isRed (c: Color) = c = Red

let shape: IShape =
    { new IShape with
        member _.Area = 1.0
        member _.Name = "one"
    }

let bumped () =
    let c = Counter()
    c.Bump()
    let n = c.Count
    c.Reset()
    n

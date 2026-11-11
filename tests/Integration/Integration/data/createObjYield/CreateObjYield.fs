module CreateObjYield

open Fable.Core.JsInterop

type Shared =
    static member inline pair = [ "a" ==> "b"; "c" ==> "d" ]

let inlineMember () =
    createObj [ "e" ==> "f"; yield! Shared.pair ]

let nested (value: int) =
    createObj [
        "a" ==> value
        yield! [ "b" ==> 2; yield! [ "c" ==> 3 ] ]
        yield! [| "d" ==> 4 |]
        "e" ==> 5
    ]

// Not known at compile time: these keep building the object at run time

let duplicateKey () =
    createObj [ "a" ==> 1; yield! [ "a" ==> 2 ] ]

let conditional (test: bool) =
    createObj [ "a" ==> 1; if test then "b" ==> 2 ]

let dynamicKey (key: string) =
    createObj [ "a" ==> 1; yield! [ key ==> 2 ] ]

let dynamicList (rest: (string * obj) list) = createObj [ "a" ==> 1; yield! rest ]

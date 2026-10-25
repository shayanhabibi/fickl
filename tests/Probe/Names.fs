module Names

open System
open Check

// Identifiers that are reserved words or globals in JavaScript
let ``class`` = 1
let undefined = 2
let arguments = 3
let eval = 4
let ``yield`` = 5
let await = 6
let ``import`` = 7
let Symbol = 8
let Object = "obj"
let Array = "arr"
let String = "str"
let Math = "math"
let Number = 9
let JSON = 10
let globalThis = 11
let window = 12
let x' = 13
let x'' = 14
let ``a b`` = 15
let ``1st`` = 16
let ``$`` = 17
let ``_`` = 18
let ``let`` = 19
let ``new`` = 20
let ``this`` = 21
let ``super`` = 22
let ``delete`` = 23
let ``typeof`` = 24
let ``instanceof`` = 25
let ``void`` = 26
let ``with`` = 27
let ``enum`` = 28
let ``static`` = 29
let ``implements`` = 30
let ``package`` = 31
let ``interface`` = 32
let NaN = 33
let Infinity = 34

// Names of fable-library helpers used by generated code
let equals a b = a + b
let compare a b = a * b
let toString (x: int) = "custom" + string x
let defaultOf () = 42
let structuralHash (x: int) = x + 1
let curry2 x = x
let uncurry2 x = x
let printf = "printf-value"
let Record = "rec"
let Union = "union"
let FSharpRef = "ref"
let some x = x
let value x = x

type Members =
    { constructor: int
      prototype: int
      __proto__: int
      toString: int
      valueOf: int
      hasOwnProperty: int
      length: int
      name: int
      tag: int
      fields: int }

type U =
    | Tag of int
    | Fields of string
    | Cases
    | Name of name: string
    member this.cases() = "cases-member"

type K() =
    static member name = "static-name"
    static member length = 3
    static member prototype = "static-prototype"
    static member caller = "static-caller"
    member _.constructor = "inst-constructor"
    member _.toString() = "inst-toString"

type Statics =
    static member call = "call"
    static member apply = "apply"
    static member bind = "bind"

module MathM =
    let max a b = a - b

let run () =
    check "names.reserved"
        (fun () ->
            string (
                ``class`` + undefined + arguments + eval + ``yield`` + await + ``import`` + Symbol + Number + JSON
                + globalThis + window + x' + x'' + ``a b`` + ``1st`` + ``$`` + ``_`` + ``let`` + ``new`` + ``this``
                + ``super`` + ``delete`` + ``typeof`` + ``instanceof`` + ``void`` + ``with`` + ``enum`` + ``static``
                + ``implements`` + ``package`` + ``interface`` + NaN + Infinity
            ))
    check "names.globals.strings" (fun () -> Object + Array + String + Math)
    check "names.globals.stillwork" (fun () -> string (Double.IsNaN nan) + string (List.length [ 1; 2 ]))
    check "names.helpers" (fun () -> string (equals 2 3) + "," + string (compare 2 3) + "," + toString 5 + "," + string (defaultOf ()))
    check "names.helpers.used" (fun () -> string ([ 1 ] = [ 1 ]) + string (Operators.compare 1 2) + string (hash 1 = hash 1))
    check "names.helpers2" (fun () -> printf + Record + Union + FSharpRef + string (some 1) + string (value 2))
    check "names.helpers.printf.still" (fun () -> sprintf "%d-%s" 1 "a")
    check "names.helpers.option.still" (fun () -> string (Some 1).IsSome + string (Option.defaultValue 0 (Some 3)))
    check "names.record.fields"
        (fun () ->
            let r =
                { constructor = 1; prototype = 2; __proto__ = 3; toString = 4; valueOf = 5
                  hasOwnProperty = 6; length = 7; name = 8; tag = 9; fields = 10 }
            string (r.constructor + r.prototype + r.__proto__ + r.toString + r.valueOf + r.hasOwnProperty + r.length + r.name + r.tag + r.fields))
    check "names.record.eq"
        (fun () ->
            let mk () =
                { constructor = 1; prototype = 2; __proto__ = 3; toString = 4; valueOf = 5
                  hasOwnProperty = 6; length = 7; name = 8; tag = 9; fields = 10 }
            string (mk () = mk ()) + string (hash (mk ()) = hash (mk ())))
    check "names.record.string"
        (fun () ->
            let r =
                { constructor = 1; prototype = 2; __proto__ = 3; toString = 4; valueOf = 5
                  hasOwnProperty = 6; length = 7; name = 8; tag = 9; fields = 10 }
            let r2 = { r with __proto__ = 99 }
            string r2.__proto__ + string r2.prototype)
    check "names.union"
        (fun () ->
            let xs = [ Tag 1; Fields "f"; Cases; Name "n" ]
            xs |> List.map (function Tag i -> string i | Fields s -> s | Cases -> "C" | Name n -> n) |> Microsoft.FSharp.Core.String.concat ",")
    check "names.union.member" (fun () -> (Tag 1).cases())
    check "names.union.eq" (fun () -> string (Tag 1 = Tag 1) + string (Name "a" < Name "b"))
    check "names.class.statics" (fun () -> K.name + string K.length + K.prototype + K.caller)
    check "names.class.inst" (fun () -> let k = K() in k.constructor + k.toString())
    check "names.class.function.statics" (fun () -> Statics.call + Statics.apply + Statics.bind)
    check "names.shadow.math" (fun () -> string (MathM.max 5 3) + string (max 5 3))
    check "names.shadow.local"
        (fun () ->
            let String = 1
            let Array = 2
            let Error = 3
            let Promise = 4
            let Map = 5
            let Set = 6
            string (String + Array + Error + Promise + Map + Set) + string ([| 1; 2 |].Length) + string (Collections.Set.count (set [ 1 ])) + string (Map)
            + string (Collections.Map.count (Collections.Map.ofList [ 1, 2 ])))
    check "names.local.reserved"
        (fun () ->
            let ``arguments`` = [ 1; 2 ]
            let f ``this`` ``class`` = ``this`` + ``class``
            string (f 1 2) + string (List.length ``arguments``))
    check "names.closure.this"
        (fun () ->
            let ``this`` = 10
            let f () = ``this`` + 1
            string (f ()))

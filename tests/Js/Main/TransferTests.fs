module Fable.Tests.Transfers

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
type private Shape =
    | Circle of radius: float
    | Rect of w: float * h: float
    | Empty

type private Wrapper = Wrapper of string

type private Model =
    { Name: string
      Shapes: Shape list
      Tags: Set<string>
      Scores: Map<string, int>
      Nested: int option option
      Missing: string option
      Price: decimal
      Pair: int * string
      Wrapped: Wrapper
      Bytes: byte[]
      Anon: {| A: int; B: Shape |} }

    member this.Total = this.Shapes.Length + Map.count this.Scores

let private model =
    { Name = "m"
      Shapes = [ Circle 1.0; Rect(2.0, 3.0); Empty ]
      Tags = set [ "b"; "a" ]
      Scores = Map [ "x", 1; "y", 2 ]
      Nested = Some None
      Missing = None
      Price = 1.5m
      Pair = (1, "one")
      Wrapped = Wrapper "w"
      Bytes = [| 1uy; 2uy |]
      Anon = {| A = 3; B = Circle 4.0 |} }

let inline private roundTrip (value: 'T) : 'T =
    Transfer.ofPlain (JS.structuredClone (Transfer.toPlain value))
#endif

let tests =
    testList "Transfers" [
#if FABLE_COMPILER
        testCase "structuredClone copies data but drops F# prototypes" <| fun () ->
            let shapes = [| Rect(1.0, 2.0) |]
            let copy = JS.structuredClone shapes
            // Matching reads only tag and fields, so it still works on the copy
            equal 2.0 (match copy.[0] with Rect(_, h) -> h | _ -> 0.0)
            equal false (emitJsExpr (copy.[0], shapes.[0]) "Object.getPrototypeOf($0) === Object.getPrototypeOf($1)")
            // An F# map or set holds its comparer, a function, which structuredClone rejects
            let threw =
                try
                    JS.structuredClone model |> ignore
                    false
                with _ ->
                    true
            equal true threw

        testCase "toPlain gives plain objects and arrays" <| fun () ->
            let plain = Transfer.toPlain model
            equal "m" (plain?Name)
            equal true (JS.Constructors.Array.isArray (plain?Shapes))
            equal "Rect" (plain?Shapes?(1)?case)
            equal 3.0 (plain?Shapes?(1)?fields?(1))
            equal [| "a"; "b" |] (unbox<string[]> plain?Tags)
            equal "y" (plain?Scores?(1)?(0))
            equal "1.5" (plain?Price)
            equal true (isNull (plain?Missing))
            equal "w" (plain?Wrapped?fields?(0))
            equal "Circle" (plain?Anon?B?case)
            equal true (emitJsExpr plain "Object.getPrototypeOf($0) === Object.prototype")

        testCase "ofPlain restores an F# value after structuredClone" <| fun () ->
            let back = roundTrip model
            equal model back
            equal 5 back.Total
            equal (Some 2) (Map.tryFind "y" back.Scores)
            equal true (Set.contains "a" back.Tags)
            equal (Some None) back.Nested
            equal 1.5m back.Price
            equal (Rect(2.0, 3.0)) back.Shapes.[1]

        testCase "Transfer.clone deep-copies an F# value" <| fun () ->
            let copy = Transfer.clone model
            equal model copy
            equal false (obj.ReferenceEquals(model, copy))
            equal 0 (compare model copy)

        testCase "Options, unions and collections round-trip on their own" <| fun () ->
            equal (Some(Some 1)) (roundTrip (Some(Some 1)))
            equal None (roundTrip (None: int option))
            equal [ Some "a"; None ] (roundTrip [ Some "a"; None ])
            equal (Rect(1.0, 2.0)) (roundTrip (Rect(1.0, 2.0)))
            equal [| Empty |] (roundTrip [| Empty |])
            equal (Map [ 1, [ "a" ] ]) (roundTrip (Map [ 1, [ "a" ] ]))
            equal (Wrapper "x") (roundTrip (Wrapper "x"))
#endif
    ]

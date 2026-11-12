module Fable.Tests.TupleTypes

open System
open Util.Testing

[<Fable.Core.StringEnum>]
type Orientation =
    | Vertical
    | [<CompiledName("Horizontal")>] Horizontal

let tests =
  testList "Tuples" [
    testCase "Tuple dereferencing can be generated" <| fun () ->
        let x = 10, true
        let y, z = x
        equal 10 y
        equal true z

    testCase "fst function can be generated" <| fun () ->
        let xy = 10., true
        fst xy |> equal 10.

    testCase "snd function can be generated" <| fun () ->
        let xy = 10., true
        snd xy |> equal true

    testCase "Tuple constructor works" <| fun () ->
        let t2 = Tuple<_,_>(2, 3)
        let t3 = Tuple.Create("a", 14, "c")
        equal (2, 3) t2
        equal ("a", 14, "c") t3

    testCase "ValueTuple constructor works" <| fun () ->
        let t2 = ValueTuple<_,_>(2, 3)
        let t3 = ValueTuple.Create("a", 14, "c")
        equal (struct (2, 3)) t2
        equal (struct ("a", 14, "c")) t3

    testCase "Can transform ref tuple to value tuple" <| fun () ->
        let t2 = (2, 3)
        t2.ToValueTuple() |> equal (struct (2, 3))

    testCase "Can transform value tuple to ref tuple" <| fun () ->
        let t3 = (struct (2, 3, "c"))
        t3.ToTuple() |> equal (2, 3, "c")

    testCase "Can create tuples of single element" <| fun () ->
        let t1 = Tuple<_> 4
        let t1b = Tuple.Create "b"
        t1.Item1 |> equal 4
        t1b.Item1 |> equal "b"

    // On TypeScript, the tuples must be typed with the enum, not string, for tsc to accept them (#442)
    testCase "Tuples can hold StringEnum cases" <| fun () ->
        let pairs = [ Vertical, 1; Horizontal, 2 ]
        let pair = (Horizontal, (Vertical, "v"))
        let lookup = Map.ofList [ Horizontal, 2 ]
        Map.find Horizontal lookup |> equal 2
        fst pairs.Head |> equal Vertical
        pair |> snd |> fst |> equal Vertical
        (Vertical <> Horizontal) |> equal true
  ]
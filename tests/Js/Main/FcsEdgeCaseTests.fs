/// fickl: behaviour that depends on changes in Fickl's FCS fork (fickle-fsharp). Each test here passes on .NET with the
/// upstream compiler, so a failure on a target points at the fork or at how Fable reads its output.
module Fable.Tests.FcsEdgeCases

open System
open Util.Testing
open Fable.Tests.FcsEdgeCasesHelper

type Money =
    {
        Cents: int
    }

    static member (+)(a: Money, b: Money) = { Cents = a.Cents + b.Cents }
    static member Zero = { Cents = 0 }
    static member DivideByInt(a: Money, n: int) = { Cents = a.Cents / n }

type Box =
    | Box of int

    static member (+)(Box a, Box b) = Box(a + b)
    static member Zero = Box 0

let inline lengthOf (x: ^T) = (^T: (member Length: int) x)

let inline zeroOf<'T when 'T: (static member Zero: 'T)> () = 'T.Zero

let inline sumWith (f: 'a -> ^b) (xs: 'a list) : ^b =
    xs |> List.map f |> List.fold (+) LanguagePrimitives.GenericZero

// No trait constraints, only comparison and equality: these take no witnesses
let inline largest (a: 'T) (b: 'T) = if a > b then a else b
let inline sameAs (a: 'T) (b: 'T) = a = b

let inline mixed (label: 'L) (x: ^N) = sprintf "%A=%s" label (string (x + x))

type Ordered(z: int, a: string) =
    member _.Z = z
    member _.A = a
    member val M = true with get, set

type OrderedRecord = { Zeta: int; Alpha: string; Mu: bool }

type IFirstSecond =
    abstract Second: string
    abstract First: int
    abstract Third: unit -> float

let tests =
    testList "FcsEdgeCases" [
        // FS1113: inline functions can use internal values of the same project
        testCase "Inline functions can use internal values across files" <| fun () ->
            scale 2 |> equal 21
            scaleTwice 1 |> equal 111
            triple 4 |> equal 12
            wrapInline "a" |> equal [ "a"; "a" ]

#if FABLE_COMPILER
        testCase "Public inline functions can use internal values across files" <| fun () ->
            publicScale 2 |> equal 22
#endif

        testCase "Inline functions can update internal mutable values across files" <| fun () ->
            let before = calls
            scaleTracked 3 |> equal 30
            scaleTracked 4 |> equal 40
            calls - before |> equal 2

        testCase "Inline members can use internal values across files" <| fun () ->
            let m = Meter.Create 2
            m.Value |> equal 20
            m.Scaled |> equal 200

        testCase "SRTP inline functions can use internal values across files" <| fun () ->
            addAll [ 1; 2; 3 ] |> equal 6
            addAll [ 1.5; 2.5 ] |> equal 4.0
            addAll [ { Cents = 1 }; { Cents = 2 } ] |> equal { Cents = 3 }

        // Witnesses are only built for type parameters with trait constraints
        testCase "Library SRTP functions take witnesses from user types" <| fun () ->
            List.sum [ { Cents = 5 }; { Cents = 7 } ] |> equal { Cents = 12 }
            List.average [ { Cents = 4 }; { Cents = 8 } ] |> equal { Cents = 6 }
            List.sumBy (fun x -> { Cents = x }) [ 1; 2; 3 ] |> equal { Cents = 6 }
            Array.sum [| Box 1; Box 2 |] |> equal (Box 3)

        testCase "Member constraints resolve for each type" <| fun () ->
            lengthOf "abc" |> equal 3
            lengthOf [| 1; 2 |] |> equal 2
            lengthOf [ 1; 2; 3; 4 ] |> equal 4

        testCase "Static member constraints with explicit type arguments" <| fun () ->
            zeroOf<Money> () |> equal { Cents = 0 }
            zeroOf<Box> () |> equal (Box 0)

        testCase "Constrained and unconstrained type parameters in one function" <| fun () ->
            sumWith String.length [ "a"; "bc" ] |> equal 3
            sumWith (fun x -> { Cents = x }) [ 2; 3 ] |> equal { Cents = 5 }
            mixed "x" 2 |> equal "\"x\"=4"
            mixed 1 2.5 |> equal "1=5"

        testCase "Comparison and equality need no witnesses" <| fun () ->
            largest 3 7 |> equal 7
            largest "b" "a" |> equal "b"
            largest { Cents = 1 } { Cents = 2 } |> equal { Cents = 2 }
            sameAs [ 1 ] [ 1 ] |> equal true
            sameAs (Box 1) (Box 2) |> equal false

        // Interpolated strings keep their PrintfFormat lowering
        testCase "Interpolated strings with string holes only" <| fun () ->
            let a, b = "x", "y"
            $"{a}{b}" |> equal "xy"
            $"{a}" |> equal "x"
            $"<{a}-{b}>" |> equal "<x-y>"
            $"" |> equal ""

        testCase "Interpolated strings with holes of other types" <| fun () ->
            let n, f, c = 3, 1.5, 'c'
            $"{n}{f}{c}" |> equal "31.5c"
            $"{n + 1} and {n * 2}" |> equal "4 and 6"
            $"{[ 1; 2 ]}" |> equal "[1; 2]"

        testCase "Interpolated strings with format specifiers" <| fun () ->
            let n, s, f = 42, "s", 3.14159
            $"%d{n}|%s{s}|%.2f{f}" |> equal "42|s|3.14"
            $"%5d{n}|%-5d{n}|%05d{n}" |> equal "   42|42   |00042"
            $"%x{255}|%X{255}|%o{8}" |> equal "ff|FF|10"
            $"%o{-1}|%o{0}|%5o{64}" |> equal "37777777777|0|  100"
            $"%b{true} %c{'z'}" |> equal "true z"
            $"%A{s}|%A{[ 1; 2 ]}" |> equal "\"s\"|[1; 2]"
            $"%d{n} and {s}" |> equal "42 and s"

        testCase "Interpolated strings with .NET format strings and alignment" <| fun () ->
            let f = 3.14159
            $"{f:F2}" |> equal "3.14"
            $"[{42,5}]" |> equal "[   42]"
            $"[{42,-5}]" |> equal "[42   ]"
            $"[{f,8:F3}]" |> equal "[   3.142]"
            $"{255:X4}" |> equal "00FF"

        testCase "Nested, verbatim and extended interpolated strings" <| fun () ->
            let x = 1
            $"""a{ $"b{x}" }c""" |> equal "ab1c"
            $@"\n{x}" |> equal "\\n1"
            @$"\t{x}" |> equal "\\t1"
            $$"""{{{x}}}""" |> equal "{1}"
            $$"""{x} {{x}}""" |> equal "{x} 1"
            $"{{x}} %%" |> equal "{x} %"
            $"""{"q"}""" |> equal "q"

        testCase "Interpolated strings as PrintfFormat" <| fun () ->
            let x = 5
            sprintf $"v=%d{x}" |> equal "v=5"
            String.Format("{0}", $"{x}{x}") |> equal "55"

        // Properties and fields come in declaration order
        testCase "Record fields keep declaration order" <| fun () ->
            let r = { Zeta = 1; Alpha = "a"; Mu = true }

            Reflection.FSharpType.GetRecordFields(typeof<OrderedRecord>)
            |> Array.map _.Name
            |> equal [| "Zeta"; "Alpha"; "Mu" |]

            Reflection.FSharpValue.GetRecordFields(r) |> equal [| box 1; box "a"; box true |]

        testCase "Object expressions implement interface members in any order" <| fun () ->
            let o =
                { new IFirstSecond with
                    member _.Third() = 3.0
                    member _.First = 1
                    member _.Second = "2"
                }

            o.First |> equal 1
            o.Second |> equal "2"
            o.Third() |> equal 3.0

        testCase "Class properties keep their values" <| fun () ->
            let o = Ordered(2, "a")
            o.M <- false
            (o.Z, o.A, o.M) |> equal (2, "a", false)
    ]

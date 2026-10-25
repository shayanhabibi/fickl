module Fable.Tests.Currying

open Util.Testing
open Fickl.Core

#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop
#endif

// fickl: regression tests for the implicit uncurrying of function values, and for [<Curried>],
// which keeps a parameter curried for JS that calls it one argument at a time

/// Counts first applications, so partial application is observable; one per test, as tests run in parallel
type private Counter() =
    member val Count = 0 with get, set
    member c.Tick() = c.Count <- c.Count + 1

    member c.Effectful: int -> int -> int =
        fun a ->
            c.Tick()
            fun b -> a + b

let private twice (g: int -> int) = g 2 + g 3

type private R = { F: int -> int -> int }
type private U = U of (int -> int -> int)
type private RO = { G: (int -> int -> int) option }
type private M = { mutable H: int -> int -> int }
type private Box<'T> = { V: 'T }
type private R3 = { F3: int -> int -> int -> int }
type private RL = { L: int -> (int -> int) }

type IMakeCurrying =
    abstract Make: int -> (int -> int)

let private callTwice (f: int -> int -> int) = twice (f 1)

let private ident (x: 'T) = x

let private make3 (counter: Counter) : int -> int -> int -> int =
    fun a ->
        counter.Tick()
        fun b c -> a + b + c

let callTwiceCurried ([<Curried>] f: int -> int -> int) = twice (f 1)

let applyCurried ([<Curried>] f: int -> int -> int) = f 1 2

let private forwardCurried ([<Curried>] f: int -> int -> int) = callTwice f

let private passUncurried (f: int -> int -> int) = callTwiceCurried f

let private curriedOnValue ([<Curried>] x: int) = x + 1

let inline private inlineCurried ([<Curried>] f: int -> int -> int) = f 5 6

type IRunCurried =
    abstract Run: [<Curried>] f: (int -> int -> int) -> int

type IRunCurriedDerived =
    inherit IRunCurried
    abstract Other: int

type RunCurried() =
    interface IRunCurried with
        member _.Run f = f 10 20

[<AbstractClass>]
type BaseRunCurried() =
    abstract Run: [<Curried>] f: (int -> int -> int) -> int

type DerivedRunCurried() =
    inherit BaseRunCurried()
    override _.Run f = f 2 3

[<AbstractClass>]
type BaseOverCurried() =
    abstract Pick: [<Curried>] f: (int -> int -> int) -> int
    abstract Pick: f: (string -> string -> string) -> string

type OverCurried() =
    inherit BaseOverCurried()
    override _.Pick(f: int -> int -> int) = f 1 2
    override _.Pick(f: string -> string -> string) = f "a" "b"

type IFoldCurried =
    abstract Fold<'T> : [<Curried>] f: ('T -> 'T -> 'T) -> 'T -> 'T

type TupledCurried() =
    member _.M(x: int, [<Curried>] f: int -> int -> int) = f x x

#if FABLE_COMPILER
[<Emit("(($0)(1))(2)")>]
let private emitCallsCurried ([<Curried>] f: int -> int -> int) : int = jsNative

[<Emit("($0)(1)(2)")>]
let private emitObjCallsCurried ([<Curried>] f: obj) : int = jsNative

[<Emit("($0)(1, 2)")>]
let private emitObjCallsUncurried (f: obj) : int = jsNative

[<Import("callCurried", "./js/curried.js")>]
let private importCallsCurried ([<Curried>] f: int -> int -> int) : int = jsNative

[<Import("callCurried3", "./js/curried.js")>]
let private importCallsCurried3 ([<Curried>] f: int -> int -> int -> int) : int = jsNative

let private curriedFromJs: int -> int -> int = emitJsExpr () "(a => b => a + b)"
#endif

/// Runs `f` with a fresh counter and returns its result with the number of first applications
let private counted f =
    let counter = Counter()
    let result = f counter
    result, counter.Count

let tests =
    testList "Currying" [
        testCase "Partial application through a record field applies the first argument once" <| fun () ->
            counted (fun counter -> let r = { F = counter.Effectful } in twice (r.F 1)) |> equal (7, 1)

        testCase "Partial application of a function argument applies the first argument once" <| fun () ->
            counted (fun counter -> callTwice counter.Effectful) |> equal (7, 1)

        testCase "Partial application through a union field applies the first argument once" <| fun () ->
            counted (fun counter -> let (U f) = U counter.Effectful in twice (f 1)) |> equal (7, 1)

        testCase "Partial application through an optional field applies the first argument once" <| fun () ->
            counted (fun counter ->
                match { G = Some counter.Effectful }.G with
                | Some f -> twice (f 1)
                | None -> 0
            )
            |> equal (7, 1)

        testCase "Partial application through an anonymous record applies the first argument once" <| fun () ->
            counted (fun counter -> let r = {| F = counter.Effectful |} in twice (r.F 1)) |> equal (7, 1)

        testCase "Partial application of an abstract member returning a function applies it once" <| fun () ->
            counted (fun counter ->
                let o =
                    { new IMakeCurrying with
                        member _.Make a =
                            counter.Tick()
                            fun b -> a + b }
                twice (o.Make 1)
            )
            |> equal (7, 1)

        testCase "Functions moved from a generic field to a function field keep their arity" <| fun () ->
            counted (fun counter ->
                let b = { V = counter.Effectful }
                let r = { F = b.V }
                twice (r.F 1)
            )
            |> equal (7, 1)

        testCase "Functions in a list are partially applied once" <| fun () ->
            counted (fun counter -> [ counter.Effectful ] |> List.sumBy (fun f -> twice (f 1))) |> equal (7, 1)

        testCase "Functions assigned to a mutable field are partially applied once" <| fun () ->
            counted (fun counter ->
                let m = { H = (fun a b -> a * b) }
                m.H <- counter.Effectful
                twice (m.H 1)
            )
            |> equal (7, 1)

        testCase "Functions passed through a generic identity keep their arity" <| fun () ->
            counted (fun counter -> let r = { F = ident counter.Effectful } in twice (r.F 1)) |> equal (7, 1)

        testCase "A three-argument field split after the first argument applies it once" <| fun () ->
            counted (fun counter ->
                let r = { F3 = make3 counter }
                let g = r.F3 1
                g 2 3 + g 4 5
            )
            |> equal (16, 1)

        testCase "A field typed as a function returning a function applies both arguments" <| fun () ->
            counted (fun counter -> let r = { L = counter.Effectful } in r.L 1 2 + r.L 3 4) |> equal (10, 2)

        testCase "Curried parameters keep F# semantics for F# callers" <| fun () ->
            counted (fun counter -> callTwiceCurried counter.Effectful) |> equal (7, 1)
            counted (fun counter -> passUncurried counter.Effectful) |> equal (7, 1)
            counted (fun counter -> forwardCurried counter.Effectful) |> equal (7, 1)
            counted (fun counter -> counter.Effectful |> callTwiceCurried) |> equal (7, 1)
            counted (fun counter -> let h = callTwiceCurried in h counter.Effectful) |> equal (7, 1)
            counted (fun counter -> [ counter.Effectful ] |> List.map callTwiceCurried |> List.sum) |> equal (7, 1)

            let both: ((int -> int -> int) -> int) list = [ callTwiceCurried; callTwice ]
            counted (fun counter -> both |> List.sumBy (fun f -> f counter.Effectful)) |> equal (14, 2)

        testCase "Curried parameters work on slots, overrides, overloads and methods" <| fun () ->
            let o = { new IRunCurried with member _.Run f = f 1 2 }
            o.Run (fun a b -> a + b) |> equal 3
            (RunCurried() :> IRunCurried).Run (fun a b -> a * b) |> equal 200

            let derived =
                { new IRunCurriedDerived with
                    member _.Run f = f 3 4
                    member _.Other = 0 }
            derived.Run (fun a b -> a * b) |> equal 12
            (DerivedRunCurried() :> BaseRunCurried).Run (fun a b -> a * b) |> equal 6

            let over = OverCurried() :> BaseOverCurried
            over.Pick (fun (a: int) b -> a + b) |> equal 3
            over.Pick (fun (a: string) b -> a + b) |> equal "ab"

            let folder = { new IFoldCurried with member _.Fold f x = f (f x x) x }
            folder.Fold (fun a b -> a + b) 2 |> equal 6

            TupledCurried().M(4, fun a b -> a * b) |> equal 16
            curriedOnValue 1 |> equal 2
            inlineCurried (fun a b -> a * b) |> equal 30

#if FABLE_COMPILER
        testCase "Function values from JS are uncurried where F# needs them" <| fun () ->
            curriedFromJs 1 2 |> equal 3
            let r = { F = curriedFromJs }
            r.F 1 2 |> equal 3
            emitObjCallsUncurried (box (fun (a: int) (b: int) -> a + b)) |> equal 3

        testCase "Curried parameters pass lambdas to JS one argument at a time" <| fun () ->
            emitCallsCurried (fun a b -> a + b) |> equal 3
            emitObjCallsCurried (box (fun (a: int) (b: int) -> a + b)) |> equal 3
            importCallsCurried (fun a b -> a + b) |> equal 3
            importCallsCurried3 (fun a b c -> a + b + c) |> equal 6
            emitCallsCurried (Counter().Effectful) |> equal 3

        testCase "Curried parameters accept curried functions from JS" <| fun () ->
            emitJsExpr applyCurried "$0(a => b => a + b)" |> equal 3
            emitJsExpr callTwiceCurried "$0(a => b => a + b)" |> equal 7
            emitJsExpr (RunCurried() :> IRunCurried) "$0.Run(a => b => a - b)" |> equal -10

            let folder = { new IFoldCurried with member _.Fold f x = f x x }
            emitJsExpr folder "$0.Fold(a => b => a + b, 4)" |> equal 8
#endif
    ]

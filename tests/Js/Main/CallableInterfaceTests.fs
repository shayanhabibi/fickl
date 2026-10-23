module Fable.Tests.CallableInterfaces

open Util.Testing
open Fickl.Core

#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop
#endif

[<CallableInterface>]
type Identity =
    abstract Invoke<'T>: value: 'T -> 'T

[<CallableInterface>]
type DerivedIdentity =
    inherit Identity

[<CallableInterface>]
type OtherIdentity =
    inherit Identity

[<CallableInterface>]
type DiamondIdentity =
    inherit DerivedIdentity
    inherit OtherIdentity

[<CallableInterface>]
type Pair =
    abstract Invoke<'T>: left: 'T * right: 'T -> 'T

[<CallableInterface>]
type Empty =
    abstract Invoke: unit -> int

[<CallableInterface>]
type Recursive =
    abstract Invoke: value: int -> int

type Ordinary =
    abstract Invoke: value: int -> int

[<CallableInterface>]
type WithOuter<'T> =
    abstract Invoke<'U>: left: 'T * right: 'U -> 'T * 'U

[<CallableInterface>]
type DerivedOuter<'T> =
    inherit WithOuter<'T>

[<CallableInterface>]
type ClosedOuter =
    inherit DerivedOuter<string>

[<CallableInterface>]
type OtherOuter<'T> =
    inherit WithOuter<'T>

[<CallableInterface>]
type DiamondOuter =
    inherit ClosedOuter
    inherit OtherOuter<string>

[<CallableInterface>]
type Untrack =
    abstract Invoke<'T>: callback: (unit -> 'T) -> 'T

[<CallableInterface>]
type Rest =
    abstract Invoke: [<System.ParamArray>] values: int[] -> int

[<CallableInterface>]
type BodyGeneric =
    abstract Invoke<'T>: unit -> int

let tests =
    testList "Callable interfaces" [
        testCase "A generic callable diamond retains instantiated outer and method binders" <| fun () ->
            let callback =
                { new DiamondOuter with
                    member _.Invoke<'U>(left: string, right: 'U): string * 'U = left, right }
            let left: ClosedOuter = callback
            let right: OtherOuter<string> = callback
            callback.Invoke("direct", 42) |> equal ("direct", 42)
            left.Invoke("left", true) |> equal ("left", true)
            right.Invoke("right", "value") |> equal ("right", "value")
#if FABLE_COMPILER
            emitJsExpr (callback, left, right) "$0 === $1 && $1 === $2" |> equal true
#endif

        testCase "A referenced callable diamond retains both base contracts" <| fun () ->
            let callback =
                { new Fable.Tests.DllRef.CallableContracts.DiamondIdentity with
                    member _.Invoke<'T>(value: 'T): 'T = value }
            let left: Fable.Tests.DllRef.CallableContracts.DerivedIdentity = callback
            let right: Fable.Tests.DllRef.CallableContracts.OtherIdentity = callback
            left.Invoke 42 |> equal 42
            right.Invoke "dll" |> equal "dll"
#if FABLE_COMPILER
            jsTypeof callback |> equal "function"
            emitJsExpr (callback, left, right) "$0 === $1 && $1 === $2" |> equal true
#endif

        testCase "A callable diamond preserves the shared slot and both base identities" <| fun () ->
            let callback =
                { new DiamondIdentity with
                    member _.Invoke<'T>(value: 'T): 'T = value }
            let left: DerivedIdentity = callback
            let right: OtherIdentity = callback
            callback.Invoke 42 |> equal 42
            left.Invoke "left" |> equal "left"
            right.Invoke true |> equal true
#if FABLE_COMPILER
            jsTypeof callback |> equal "function"
            emitJsExpr (callback, left, right) "$0 === $1 && $1 === $2" |> equal true
#endif

        testCase "Inherited generic arguments substitute through callable bases" <| fun () ->
            let callback =
                { new ClosedOuter with
                    member _.Invoke<'U>(left: string, right: 'U): string * 'U = left, right }
            let parent: WithOuter<string> = callback
            callback.Invoke("closed", 42) |> equal ("closed", 42)
            parent.Invoke("base", true) |> equal ("base", true)
#if FABLE_COMPILER
            jsTypeof callback |> equal "function"
            emitJsExpr (callback, parent) "$0 === $1" |> equal true
#endif

        testCase "Inherited callable contracts preserve base assignability" <| fun () ->
            let callback =
                { new DerivedIdentity with
                    member _.Invoke<'T>(value: 'T): 'T = value }
            let parent: Identity = callback
            callback.Invoke 42 |> equal 42
            parent.Invoke "base" |> equal "base"
#if FABLE_COMPILER
            jsTypeof callback |> equal "function"
            emitJsExpr (callback, parent) "$0 === $1" |> equal true
            emitJsExpr callback "$0(42)" |> equal 42
#endif

        testCase "Rest parameters collect JavaScript arguments" <| fun () ->
            let callback = { new Rest with member _.Invoke values = Array.sum values }
            callback.Invoke(1, 2, 3) |> equal 6
#if FABLE_COMPILER
            emitJsExpr callback "$0(4,5,6)" |> equal 15
#endif

        testCase "Body-only generic binders survive construction" <| fun () ->
            let callback =
                { new BodyGeneric with
                    member _.Invoke<'T>() =
                        let values = ResizeArray<'T>()
                        values.Add(Unchecked.defaultof<'T>)
                        values.Count }
            callback.Invoke<int>() |> equal 1
            callback.Invoke<string>() |> equal 1

        testCase "Outer and method generic parameters stay distinct" <| fun () ->
            let callback =
                { new WithOuter<string> with
                    member _.Invoke<'U>(left: string, right: 'U): string * 'U = left, right }
            callback.Invoke("left", 42) |> equal ("left", 42)
            callback.Invoke("left", true) |> equal ("left", true)

        testCase "Generic callbacks accept and execute function arguments" <| fun () ->
            let untrack =
                { new Untrack with
                    member _.Invoke<'T>(callback: unit -> 'T): 'T = callback() }
            untrack.Invoke(fun () -> 42) |> equal 42
            untrack.Invoke(fun () -> "hello") |> equal "hello"
#if FABLE_COMPILER
            emitJsExpr untrack "$0(() => 42)" |> equal 42
#endif

        testCase "Generic parameters remain method-owned" <| fun () ->
            let callback =
                { new Identity with
                    member _.Invoke<'T>(value: 'T): 'T = value }
            callback.Invoke 42 |> equal 42
            callback.Invoke "hello" |> equal "hello"
#if FABLE_COMPILER
            jsTypeof callback |> equal "function"
            emitJsExpr callback "$0(42)" |> equal 42
            emitJsExpr callback "$0('hello')" |> equal "hello"
#endif

        testCase "Multiple arguments retain closure capture" <| fun () ->
            let mutable calls = 0
            let callback =
                { new Pair with
                    member _.Invoke<'T>(left: 'T, right: 'T): 'T =
                        calls <- calls + 1
                        right }
            callback.Invoke(1, 2) |> equal 2
            callback.Invoke("left", "right") |> equal "right"
            calls |> equal 2
#if FABLE_COMPILER
            emitJsExpr callback "$0(3,7)" |> equal 7
#endif

        testCase "Zero argument invocation" <| fun () ->
            let callback = { new Empty with member _.Invoke() = 42 }
            callback.Invoke() |> equal 42
#if FABLE_COMPILER
            emitJsExpr callback "$0()" |> equal 42
#endif

        testCase "Recursive self calls remain bound" <| fun () ->
            let factorial =
                { new Recursive with
                    member self.Invoke value =
                        if value <= 1 then 1 else value * self.Invoke(value - 1) }
            factorial.Invoke 5 |> equal 120

        testCase "Ordinary interfaces remain objects" <| fun () ->
            let callback = { new Ordinary with member _.Invoke value = value + 1 }
            callback.Invoke 3 |> equal 4
#if FABLE_COMPILER
            jsTypeof callback |> equal "object"
#endif

        testCase "Referenced inherited contracts preserve construction and base identity" <| fun () ->
            let callback =
                { new Fable.Tests.DllRef.CallableContracts.DerivedIdentity with
                    member _.Invoke<'T>(value: 'T): 'T = value }
            let parent: Fable.Tests.DllRef.CallableContracts.Identity = callback
            callback.Invoke 7 |> equal 7
            parent.Invoke "dll-base" |> equal "dll-base"
#if FABLE_COMPILER
            jsTypeof callback |> equal "function"
            emitJsExpr (callback, parent) "$0 === $1" |> equal true
#endif

        testCase "Referenced assembly contracts preserve generic construction and calls" <| fun () ->
            let callback =
                { new Fable.Tests.DllRef.CallableContracts.Identity with
                    member _.Invoke<'T>(value: 'T): 'T = value }
            callback.Invoke 7 |> equal 7
            callback.Invoke "dll" |> equal "dll"
#if FABLE_COMPILER
            jsTypeof callback |> equal "function"
            emitJsExpr callback "$0(7)" |> equal 7
#endif

#if FABLE_COMPILER
        testCase "Native callbacks retain identity through generic calls" <| fun () ->
            let callback: Identity = emitJsExpr () "(value => value)"
            let same = callback
            callback.Invoke 7 |> equal 7
            callback.Invoke "native" |> equal "native"
            emitJsExpr (callback, same) "$0 === $1" |> equal true
            emitJsExpr callback "(fn => fn(7) === 7 && fn('native') === 'native')($0)" |> equal true
#endif
    ]

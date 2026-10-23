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

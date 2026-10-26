module Fable.Tests.SymbolKeys

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core
open Util.Testing
open Fable.Tests.SymbolKeyBindings

type SymbolBox(value: int) =
    [<SymbolKey("Symbol.toPrimitive")>]
    member _.ToPrimitive(_hint: string) = value

    [<SymbolKey("Symbol.for('fickl.test.static')")>]
    static member StaticValue = 42

type ImportedBox() =
    [<SymbolKey("state", "./js/symbolKeys.js")>]
    member val Value = 1 with get, set

type OverloadedSymbols() =
    [<SymbolKey("Symbol.for('fickl.test.int')")>]
    member _.Convert(value: int) = value + 1
    [<SymbolKey("Symbol.for('fickl.test.string')")>]
    member _.Convert(value: string) = value + "!"

type SymbolImplementation() =
    let mutable state = 1
    interface IPrimitive with member _.ToPrimitive(_hint) = 42
    interface IState with member _.State with get() = state and set value = state <- value

[<AbstractClass>]
type AbstractSymbol() =
    [<SymbolKey("Symbol.toPrimitive")>]
    abstract ToPrimitive: hint: string -> int

type DerivedSymbol() =
    inherit AbstractSymbol()
    override _.ToPrimitive(_hint) = 42

type StaticSymbolFields() =
    [<DefaultValue; SymbolKey("Symbol.for('fickl.test.static.field')")>]
    static val mutable private Value: int
    static member GetSet(value: int) =
        StaticSymbolFields.Value <- value
        StaticSymbolFields.Value

type SymbolDisposable(nativeDispose: unit -> unit, fsDispose: unit -> unit) =
    [<SymbolKey("Symbol.dispose")>]
    member _.Release() = nativeDispose()
    interface System.IDisposable with member _.Dispose() = fsDispose()

type ImportedRecord = {
    [<SymbolKey("state", "./js/symbolKeys.js")>]
    Value: int
}

type SymbolRecord = {
    First: int
    [<SymbolKey("Symbol.for('fickl.test.state')")>]
    mutable State: int
    Last: int
}

#if FABLE_COMPILER
[<Import("readState", "./js/symbolKeys.js")>]
let private readState (value: obj) : int = jsNative
#endif

let tests =
    testList "SymbolKeys" [
#if FABLE_COMPILER
        testCase "symbol expressions preserve literal Emit placeholder text" <| fun () ->
            let value = { new IDollarKey with member _.State = "literal" }
            let key: obj = emitJsExpr "fickl.$0.state" "Symbol.for($0)"
            emitJsExpr<string> (value, key) "Reflect.get($0, $1)" |> equal "literal"
            value.State |> equal "literal"

        testCase "symbol interface slots emit native object methods and calls" <| fun () ->
            let value = { new IPrimitive with member _.ToPrimitive(_hint) = 42 }
            emitJsExpr<int> value "+$0" |> equal 42
            value.ToPrimitive("number") |> equal 42

        testCase "individual symbol class methods attach without AttachMembers" <| fun () ->
            let value = SymbolBox(42)
            emitJsExpr<int> value "+$0" |> equal 42
            value.ToPrimitive("number") |> equal 42

        testCase "symbol interface properties preserve reads and writes" <| fun () ->
            let mutable state = 1
            let value = { new IState with member _.State with get() = state and set value = state <- value }
            value.State <- 42
            emitJsExpr<int> value "Reflect.get($0, Symbol.for('fickl.test.state'))" |> equal 42
            value.State |> equal 42

        testCase "imported symbol keys agree across interfaces classes and records" <| fun () ->
            let mutable state = 1
            let slot = { new IImportedState with member _.Value with get() = state and set value = state <- value }
            slot.Value <- 42
            readState slot |> equal 42
            slot.Value |> equal 42
            let box = ImportedBox()
            box.Value <- 43
            readState box |> equal 43
            box.Value |> equal 43
            let record: ImportedRecord = { Value = 44 }
            readState record |> equal 44
            record.Value |> equal 44

        testCase "static symbol members preserve declaration keys at access sites" <| fun () ->
            SymbolBox.StaticValue |> equal 42

        testCase "overloaded symbol methods retain their resolved slots" <| fun () ->
            let value = OverloadedSymbols()
            value.Convert(41) |> equal 42
            value.Convert("hello") |> equal "hello!"

        testCase "class interface implementations retain symbol slots" <| fun () ->
            let value = SymbolImplementation()
            emitJsExpr<int> value "+$0" |> equal 42
            let state = value :> IState
            state.State <- 43
            state.State |> equal 43

        testCase "symbol overrides retain inherited slots through base and derived types" <| fun () ->
            let value = DerivedSymbol()
            value.ToPrimitive("number") |> equal 42
            (value :> AbstractSymbol).ToPrimitive("number") |> equal 42
            emitJsExpr<int> value "+$0" |> equal 42

        testCase "static symbol fields preserve native storage on writes and reads" <| fun () ->
            StaticSymbolFields.GetSet(42) |> equal 42
            let constructor = jsConstructor<StaticSymbolFields>
            emitJsExpr<int> constructor "Reflect.get($0, Symbol.for('fickl.test.static.field'))" |> equal 42

        testCase "symbol disposal methods take precedence over generated protocol bridges" <| fun () ->
            let mutable nativeCalls = 0
            let mutable fsCalls = 0
            let value = SymbolDisposable((fun () -> nativeCalls <- nativeCalls + 1), (fun () -> fsCalls <- fsCalls + 1))
            emitJsStatement value "$0[Symbol.dispose]()"
            nativeCalls |> equal 1
            fsCalls |> equal 0

        testCase "inherited symbol slots preserve keys across files" <| fun () ->
            let mutable state = 0
            let mutable imported = 0
            let value = { new IChild with
                member _.ToPrimitive(_hint) = 42
                member _.State with get() = state and set value = state <- value
                member _.Value with get() = imported and set value = imported <- value }
            value.ToPrimitive("number") |> equal 42
            value.State <- 43
            value.State |> equal 43
            value.Value <- 44
            value.Value |> equal 44
            readState value |> equal 44
#endif
        testCase "symbol record fields preserve mutation and copies" <| fun () ->
            let value = { First = 1; State = 2; Last = 3 }
            value.State <- 42
            let copy = { value with Last = 4 }
            copy.State |> equal 42
            value.Last |> equal 3
#if FABLE_COMPILER
            emitJsExpr<int> value "Reflect.get($0, Symbol.for('fickl.test.state'))" |> equal 42
            emitJsExpr<int> value "Object.getOwnPropertySymbols($0).length" |> equal 1
            emitJsExpr<bool> value "Object.prototype.hasOwnProperty.call($0, 'State')" |> equal false
            emitJsExpr<int> value "JSON.parse(JSON.stringify($0)).State" |> equal 42
#endif
        testCase "symbol record fields participate in structural operations in declaration order" <| fun () ->
            let first = { First = 1; State = 2; Last = 9 }
            let second = { First = 1; State = 3; Last = 0 }
            first = second |> equal false
            compare first second |> equal -1
            hash first = hash second |> equal false
            string first |> equal "{ First = 1\n  State = 2\n  Last = 9 }"

        testCase "symbol record reflection preserves names and values" <| fun () ->
            let value = { First = 1; State = 42; Last = 3 }
            let fields = Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(typeof<SymbolRecord>)
            fields |> Array.map _.Name |> equal [|"First"; "State"; "Last"|]
            Microsoft.FSharp.Reflection.FSharpValue.GetRecordField(value, fields[1]) |> unbox<int> |> equal 42
            Microsoft.FSharp.Reflection.FSharpValue.GetRecordFields(value) |> Array.map unbox<int> |> equal [|1;42;3|]
            let boxed: obj = box value
            Microsoft.FSharp.Reflection.FSharpValue.GetRecordFields(boxed) |> Array.map unbox<int> |> equal [|1;42;3|]
    ]

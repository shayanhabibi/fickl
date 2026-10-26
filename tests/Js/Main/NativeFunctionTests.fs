module Fable.Tests.NativeFunctions

open Fable.Core.JsInterop
open Fickl.Core.JsInterop
open Util.Testing

type Receiver =
    abstract tag: string

type Callbacks = { run: int -> int -> int }

#if FABLE_COMPILER
[<Fable.Core.Global("decodeURIComponent")>]
let private decodeNative (value: string) : string = Fable.Core.Util.nativeOnly

[<Fable.Core.Emit("$0.call($1, $2, $3)")>]
let private invokeWithReceiver (callback: int -> int -> string) (receiver: Receiver) (a: int) (b: int) : string =
    Fable.Core.Util.nativeOnly

let private receiverIdentity<'T> () = withThis (fun (self: 'T) () -> self)

[<Fable.Core.Emit("$0.call({ tag: 'attribute' }, 3, 4)")>]
let private invokeOrdinary ([<Fickl.Core.JsFunction>] callback: int -> int -> string) : string =
    Fable.Core.Util.nativeOnly
#endif

let tests =
    testList "NativeFunctions" [
#if FABLE_COMPILER
        testCase "callback parameter attributes select ordinary functions" <| fun () ->
            invokeOrdinary (fun a b -> (jsThis<Receiver>).tag + string (a + b)) |> equal "attribute7"
            invokeOrdinary (withThis (fun (self: Receiver) a b -> self.tag + string (a + b))) |> equal "attribute7"

        testCase "ordinary callbacks have a dynamic receiver and a prototype" <| fun () ->
            let suffix = "!"
            let callback = jsFunction (fun (_: int) -> (jsThis<Receiver>).tag + suffix)
            emitJsExpr callback "({ tag: 'native', callback: $0 }).callback(1)" |> equal "native!"
            emitJsExpr callback "Object.prototype.hasOwnProperty.call($0, 'prototype')" |> equal true

        testCase "ordinary callbacks have their own arguments" <| fun () ->
            let callback = jsFunction (fun (_: int) -> emitJsExpr () "arguments.length")
            emitJsExpr callback "Reflect.apply($0, undefined, [1, 2, 3])" |> equal 3

        testCase "ordinary forwarding callbacks retain their function identity" <| fun () ->
            let callback = jsFunction (fun value -> decodeNative value)
            callback "hello%20world" |> equal "hello world"
            emitJsExpr callback "Object.prototype.hasOwnProperty.call($0, 'prototype')" |> equal true
            emitJsExpr callback "$0 === decodeURIComponent" |> equal false

        testCase "typed receivers are not ordinary arguments" <| fun () ->
            let callback = withThis (fun (self: Receiver) (n: int) -> self.tag + string n)
            emitJsExpr callback "$0.call({ tag: 'value' }, 7)" |> equal "value7"
            emitJsExpr callback "$0.length" |> equal 1

        testCase "typed receivers survive nested ordinary callbacks" <| fun () ->
            let callback = withThis (fun (self: Receiver) (_: int) ->
                jsFunction (fun (_: int) -> self.tag))
            let nested: int -> string = emitJsExpr callback "$0.call({ tag: 'outer' }, 1)"
            emitJsExpr nested "({ tag: 'inner', callback: $0 }).callback(2)" |> equal "outer"

        testCase "ordinary callbacks can initialize constructor instances" <| fun () ->
            let callback = withThis (fun (self: obj) (n: int) -> emitJsStatement (self, n) "$0.value = $1")
            emitJsExpr callback "Reflect.construct($0, [42]).value" |> equal 42

        testCase "callbacks returned from branches keep ordinary function semantics" <| fun () ->
            let make enabled =
                if enabled then jsFunction (fun (_: int) -> (jsThis<Receiver>).tag)
                else jsFunction (fun (_: int) -> "disabled")
            emitJsExpr (make true) "({ tag: 'branch', callback: $0 }).callback(1)" |> equal "branch"

        testCase "unmarked callbacks remain arrows" <| fun () ->
            let callback = fun (n: int) -> n + 1
            emitJsExpr callback "Object.prototype.hasOwnProperty.call($0, 'prototype')" |> equal false

        testCase "ordinary callbacks keep multiple arguments and FSharp calls" <| fun () ->
            let callback = jsFunction (fun (a: int) (b: int) -> a + b)
            callback 2 3 |> equal 5
            let addTwo = callback 2
            addTwo 4 |> equal 6
            addTwo 5 |> equal 7
            emitJsExpr callback "$0.length" |> equal 2

        testCase "ignoring an ordinary callback still produces unit" <| fun () ->
            emitJsExpr (box (ignore (jsFunction (fun (a: int) (b: int) -> a + b)))) "$0 === undefined"
            |> equal true

        testCase "immediate calls keep their own arguments" <| fun () ->
            jsFunction (fun (_: int) -> emitJsExpr () "arguments.length") 1 |> equal 1

        testCase "typed unit callbacks have no ordinary parameters" <| fun () ->
            let callback = withThis (fun (self: Receiver) () -> self.tag)
            emitJsExpr callback "$0.call({ tag: 'unit' })" |> equal "unit"
            emitJsExpr callback "$0.length" |> equal 0

        testCase "ordinary callbacks keep their ABI through typed record fields" <| fun () ->
            let callbacks = { run = jsFunction (fun (a: int) (b: int) -> a + b) }
            callbacks.run 3 4 |> equal 7
            emitJsExpr callbacks "$0.run(4, 5)" |> equal 9

        testCase "typed receiver callbacks keep multiple ordinary arguments through bindings" <| fun () ->
            let callback = withThis (fun (self: Receiver) (a: int) (b: int) -> self.tag + string (a + b))
            let receiver = { new Receiver with member _.tag = "sum" }
            invokeWithReceiver callback receiver 3 4 |> equal "sum7"

        testCase "receiver annotations retain enclosing generic parameters" <| fun () ->
            emitJsExpr (receiverIdentity<Receiver> ()) "$0.call({ tag: 'generic' }).tag" |> equal "generic"
#endif
    ]

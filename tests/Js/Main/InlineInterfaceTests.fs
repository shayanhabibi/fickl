module Fable.Tests.InlineInterfaces

open Fable.Core
open Fickl.Core
open Util.Testing

type Counter =
    abstract count: int
    abstract inc: unit -> unit

type CounterClass() =
    let mutable count = 0
    interface Counter with
        member _.count = count
        member _.inc() = count <- count + 1

[<RequireQualifiedAccess; TypeScriptTaggedUnion("type")>]
type Message =
    | [<InlineInterface>] Count of Counter
    | Nothing

type Text =
    abstract ``type``: string
    abstract text: string

[<RequireQualifiedAccess; TypeScriptTaggedUnion("type")>]
type Content =
    | [<InlineInterface>] Text of Text
    | Empty

[<RequireQualifiedAccess; TypeScriptTaggedUnion("type"); InlineInterface(InlineInterfaceRepresentation.Copy)>]
type CopiedMessage =
    | Count of Counter
    | Nothing

[<RequireQualifiedAccess; TypeScriptTaggedUnion("type")>]
type PlainCopiedMessage =
    | [<InlineInterface(InlineInterfaceRepresentation.CopyPlainObjects)>] Count of Counter
    | Nothing

let private read = function Message.Count counter -> counter.count | Message.Nothing -> -1
let private payload = function Message.Count counter -> box counter | Message.Nothing -> null

let tests =
    testList "InlineInterfaces" [
        testCase "inline interface payload shares mutable state with the original" <| fun () ->
            let counter = CounterClass() :> Counter
            let message = Message.Count counter
            counter.inc()
            read message |> equal 1
            match message with
            | Message.Count value -> value.inc()
            | _ -> failwith "Expected Count"
            counter.count |> equal 2
            read message |> equal 2

        testCase "inline interface payload retains reference identity" <| fun () ->
            let counter = CounterClass() :> Counter
            let message = Message.Count counter
            obj.ReferenceEquals(payload message, counter) |> equal true
            let dictionary = System.Collections.Generic.Dictionary<obj, string>()
            dictionary.Add(counter, "original")
            dictionary[payload message] |> equal "original"

        testCase "inline interface payload carrying its tag reads its fields" <| fun () ->
            let text = { new Text with
                            member _.``type`` = "text"
                            member _.text = "hello" }
            match Content.Text text with
            | Content.Text value ->
                value.text |> equal "hello"
                obj.ReferenceEquals(value, text) |> equal true
            | Content.Empty -> failwith "Expected Text"

        testCase "copied inline interface payload shares state with the original through the field" <| fun () ->
            let counter = CounterClass() :> Counter
            let message = CopiedMessage.Count counter
            counter.inc()
            match message with
            | CopiedMessage.Count value ->
                value.count |> equal 1
                obj.ReferenceEquals(value, counter) |> equal true
            | CopiedMessage.Nothing -> failwith "Expected Count"
        testCase "inline interface construction keeps the union type for other case arms" <| fun () ->
            let counter = CounterClass() :> Counter
            match Message.Count counter with
            | Message.Count value -> value.count |> equal 0
            | Message.Nothing -> failwith "Expected Count"
            match Message.Nothing with
            | Message.Count _ -> failwith "Expected Nothing"
            | Message.Nothing -> ()

#if FABLE_COMPILER
        testCase "inline interface payload carrying its tag is the case value and can be cloned" <| fun () ->
            let text: Text = JsInterop.emitJsExpr () "({ type: 'text', text: 'hello' })"
            let message = Content.Text text
            obj.ReferenceEquals(box message, box text) |> equal true
            JsInterop.emitJsExpr message "Reflect.get(structuredClone($0), 'text')" |> equal "hello"
            JsInterop.emitJsExpr message "Object.isFrozen(Object.freeze($0))" |> equal true

        testCase "inline interface payload with a different tag value is not held as the case value" <| fun () ->
            let text: Text = JsInterop.emitJsExpr () "({ type: 'image', text: 'hello' })"
            let message = Content.Text text
            obj.ReferenceEquals(box message, box text) |> equal false
            JsInterop.emitJsExpr message "$0.type" |> equal "text"
            JsInterop.emitJsExpr text "$0.type" |> equal "image"
            let copied = CopiedMessage.Count(JsInterop.emitJsExpr () "({ type: 'other', count: 1, inc() {} })")
            JsInterop.emitJsExpr copied "$0.type" |> equal "count"

        testCase "copied inline interface value can be cloned and frozen" <| fun () ->
            let counter = CounterClass() :> Counter
            let message = CopiedMessage.Count counter
            JsInterop.emitJsExpr message "structuredClone($0).type" |> equal "count"
            JsInterop.emitJsExpr message "Object.isFrozen(Object.freeze($0))" |> equal true
            JsInterop.emitJsExpr counter "Reflect.get($0, 'type') === undefined" |> equal true

        testCase "CopyPlainObjects copies plain objects and views class instances" <| fun () ->
            let plain: Counter = JsInterop.emitJsExpr () "({ count: 3, inc() {} })"
            let plainMessage = PlainCopiedMessage.Count plain
            JsInterop.emitJsExpr plainMessage "Object.isFrozen(Object.freeze($0))" |> equal true
            JsInterop.emitJsExpr plainMessage "$0.type" |> equal "count"
            match plainMessage with
            | PlainCopiedMessage.Count value -> obj.ReferenceEquals(value, plain) |> equal true
            | PlainCopiedMessage.Nothing -> failwith "Expected Count"
            let counter = CounterClass() :> Counter
            let classMessage = PlainCopiedMessage.Count counter
            counter.inc()
            JsInterop.emitJsExpr classMessage "Reflect.get($0, 'count')" |> equal 1

        testCase "inline interface JavaScript view forwards state and keeps the tag separate" <| fun () ->
            let counter = CounterClass() :> Counter
            let message = Message.Count counter
            counter.inc()
            JsInterop.emitJsExpr message "Reflect.get($0, 'count')" |> equal 1
            JsInterop.emitJsStatement message "Reflect.get($0, 'inc').call($0)"
            counter.count |> equal 2
            JsInterop.emitJsExpr message "$0.type" |> equal "count"
            JsInterop.emitJsExpr counter "Reflect.get($0, 'type') === undefined" |> equal true
            JsInterop.emitJsExpr (message, counter) "Object.getPrototypeOf($0) === Object.getPrototypeOf($1)"
            |> equal true

        testCase "inline interface supports native private fields and frozen payloads" <| fun () ->
            let counter: Counter =
                JsInterop.emitJsExpr () "new (class { #n = 0; get count() { return this.#n; } inc() { this.#n++; } })()"
            let message = Message.Count counter
            JsInterop.emitJsStatement message "Reflect.get($0, 'inc').call($0)"
            read message |> equal 1
            obj.ReferenceEquals(payload message, counter) |> equal true
            let frozen: Counter =
                JsInterop.emitJsExpr () "Object.freeze({ count: 9, inc() {} })"
            let frozenMessage = Message.Count frozen
            JsInterop.emitJsExpr frozenMessage "Reflect.get($0, 'count')" |> equal 9
            JsInterop.emitJsExpr frozenMessage "Object.keys($0).sort()" |> equal [| "count"; "inc"; "type" |]
            obj.ReferenceEquals(payload frozenMessage, frozen) |> equal true

        testCase "inline interface forwards nonconfigurable properties without violating proxy invariants" <| fun () ->
            let counter = CounterClass() :> Counter
            let message = Message.Count counter
            JsInterop.emitJsStatement message "Object.defineProperty($0, 'extra', {value: 7, writable: true, enumerable: true, configurable: false})"
            JsInterop.emitJsExpr counter "Reflect.get($0, 'extra')" |> equal 7
            JsInterop.emitJsExpr message "Object.getOwnPropertyDescriptor($0, 'extra')?.configurable === false" |> equal true
            JsInterop.emitJsStatement message "Reflect.set($0, 'extra', 8)"
            JsInterop.emitJsExpr counter "Reflect.get($0, 'extra')" |> equal 8
            JsInterop.emitJsStatement message "Object.defineProperty($0, 'extra', {writable: false})"
            JsInterop.emitJsExpr message "Reflect.get($0, 'extra')" |> equal 8
            JsInterop.emitJsExpr message "Object.keys($0).filter(k => k === 'extra').length" |> equal 1

        testCase "inline interface fixed function descriptors retain their installed receiver" <| fun () ->
            let counter: Counter =
                JsInterop.emitJsExpr () "new (class { #n = 0; get count() { return this.#n; } inc() { this.#n++; } })()"
            let message = Message.Count counter
            JsInterop.emitJsStatement (message, counter) "Object.defineProperty($0, 'inc', {value: $1.inc.bind($1), configurable: false, writable: false})"
            JsInterop.emitJsStatement message "Reflect.get($0, 'inc').call($0)"
            counter.count |> equal 1
            JsInterop.emitJsExpr message "Object.getOwnPropertyDescriptor($0, 'inc')?.value === Reflect.get($0, 'inc')" |> equal true
#endif
    ]

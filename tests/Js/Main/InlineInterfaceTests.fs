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

#if FABLE_COMPILER
        testCase "inline interface JavaScript view forwards state and keeps the tag separate" <| fun () ->
            let counter = CounterClass() :> Counter
            let message = Message.Count counter
            counter.inc()
            JsInterop.emitJsExpr message "$0.count" |> equal 1
            JsInterop.emitJsStatement message "$0.inc()"
            counter.count |> equal 2
            JsInterop.emitJsExpr message "$0.type" |> equal "count"
            JsInterop.emitJsExpr counter "Reflect.get($0, 'type') === undefined" |> equal true
            JsInterop.emitJsExpr (message, counter) "Object.getPrototypeOf($0) === Object.getPrototypeOf($1)"
            |> equal true

        testCase "inline interface supports native private fields and frozen payloads" <| fun () ->
            let counter: Counter =
                JsInterop.emitJsExpr () "new (class { #n = 0; get count() { return this.#n; } inc() { this.#n++; } })()"
            let message = Message.Count counter
            JsInterop.emitJsStatement message "$0.inc()"
            read message |> equal 1
            obj.ReferenceEquals(payload message, counter) |> equal true
            let frozen: Counter =
                JsInterop.emitJsExpr () "Object.freeze({ count: 9, inc() {} })"
            let frozenMessage = Message.Count frozen
            JsInterop.emitJsExpr frozenMessage "$0.count" |> equal 9
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
            JsInterop.emitJsStatement message "$0.inc()"
            counter.count |> equal 1
            JsInterop.emitJsExpr message "Object.getOwnPropertyDescriptor($0, 'inc')?.value === $0.inc" |> equal true
#endif
    ]

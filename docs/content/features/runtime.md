---
title: Runtime APIs
description: Use JavaScript events, weak references, proxies, structured cloning, typed-array views and import attributes from F#.
---

This page covers the `Fickl.Core` bindings and compiler support for JavaScript runtime features: DOM and Node.js events as F# events, `WeakRef` and `FinalizationRegistry`, `Proxy` and `Reflect`, copying F# values through `structuredClone` and `postMessage`, typed-array views, and import attributes. Each section is independent, so read the ones you need.

## Events

`JsEvent.ofTarget target "click"` is an F# `IEvent` over `addEventListener`. The `Event` and `Observable` functions apply to it, and disposing a subscription or removing a handler calls `removeEventListener`. Nothing listens while the event has no handlers.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<Global("document.body")>]
let body: JS.EventTarget = jsNative

let clicks: IEvent<JS.Event> = JsEvent.ofTarget body "click"

let watch () =
    clicks
    |> Observable.filter (fun e -> not e.defaultPrevented)
    |> Observable.subscribe (fun e -> printfn "clicked %s" e.``type``)
```

The type argument of the event, here `JS.Event`, is the type of the event object, and nothing checks it. `JsEvent.ofTargetWith options target "scroll"` listens with `addEventListener` options such as `passive` or `capture`, built with `jsOptions<JS.AddEventListenerOptions>`. A `JS.AbortSignal` is an `EventTarget` too, so `JsEvent.ofTarget signal "abort"` is its abort event.

### Node.js emitters

`JsEvent.ofEmitter` does the same over an object with `on` and `off` (or `removeListener`) methods, such as a Node.js `EventEmitter`, stream or socket.

```fsharp fickl
open Fickl.Core

let lines (stream: obj) : IEvent<string> = JsEvent.ofEmitter stream "data"

let print (stream: obj) =
    (lines stream).Add(fun line -> printfn "%s" line)
```

The F# event's value is the first argument the emitter passes; the others are dropped.

### Dispatching events from an F# class

An F# class that inherits `JS.EventTarget` is a JavaScript `EventTarget`, so JavaScript code subscribes to it with `addEventListener`. `JsEvent.dispatch this "change" value` dispatches a `CustomEvent` with the value as its `detail`, and returns `false` if a listener cancelled the event.

```fsharp fickl
open Fickl.Core

type Store() =
    inherit JS.EventTarget()
    let mutable value = 0
    member this.Changed: IEvent<JS.CustomEvent<int>> = JsEvent.ofTarget this "change"
    member this.Set(next: int) =
        value <- next
        JsEvent.dispatch this "change" next |> ignore
```

`Store` compiles to `class Store extends EventTarget`. Because its base is a JavaScript class, its `let` fields are ES `#private` fields, here `#value`. `JS.Event`, `JS.CustomEvent<'T>` and the listener options bind the web types.

## Weak references

`System.WeakReference<'T>` compiles to a native `WeakRef`, typed `WeakRef<T>` in TypeScript, and `TryGetTarget` works as on .NET. `JS.FinalizationRegistry` calls back with a held value some time after a registered object is collected.

```fsharp fickl
open System
open Fickl.Core

type Node(name: string) =
    member _.Name = name

let track (node: Node) = WeakReference<Node>(node)

let tryName (reference: WeakReference<Node>) =
    match reference.TryGetTarget() with
    | true, node -> Some node.Name
    | _ -> None

let registry = JS.FinalizationRegistry(fun (held: string) -> printfn "collected %s" held)

let watch (node: Node) = registry.register (node, node.Name)
```

`SetTarget` is a compile error, as a `WeakRef` cannot be retargeted. `JS.WeakRef<'T>` binds the JavaScript type directly, with `deref` returning an option.

:::warning
The finalization callback may run late or never, for example when the program ends first. Use it to release resources that are also safe to leak, not for logic the program depends on. The held value must not be the target, or it keeps the target alive.
:::

## Proxies

`JS.Proxy.create target handler` is `new Proxy(target, handler)`, typed as its target. `JS.ProxyHandler.create` builds the handler as an object literal with only the traps you set, checked against `ProxyHandler<T>` in TypeScript. In each trap, the `JS.Reflect` member of the same name performs the default operation.

```fsharp fickl
open Fable.Core
open Fickl.Core

type Point = { X: int; Y: int }

let logged (point: Point) : Point =
    JS.Proxy.create
        point
        (JS.ProxyHandler.create (fun h ->
            h.get <-
                fun target key receiver ->
                    match key with
                    | U3.Case1 name -> printfn "read %s" name
                    | _ -> ()
                    JS.Reflect.get (target, key, receiver)))
```

A key is a `JS.PropertyKey`, the TypeScript `string | number | symbol`. A trap receives a string or a symbol; a number key arrives as its string. `JS.Proxy.revocable` returns a proxy with a `revoke` function, and `JS.Symbol` binds `Symbol()` (as `JS.Symbol.create`) and `Symbol.for`.

Nothing checks that the traps keep to the target's type: a `get` trap can return anything, and F# still sees a `Point`. A `set` trap that returns `false` makes the write throw a `TypeError`, as ES modules run in strict mode.

## Copying F# values with `structuredClone`

`structuredClone`, `postMessage`, `BroadcastChannel` and IndexedDB copy the data of a value but drop its prototypes, so an F# record, union, list, map or set is not a working F# value after the copy. An F# map or set, which holds its comparer function, throws a `DataCloneError`.

`Transfer.toPlain value` copies an F# value into plain objects and arrays by its type, and `Transfer.ofPlain<'T> data` rebuilds it with its prototypes. `Transfer.clone` does both around `structuredClone`.

```fsharp fickl
open Fickl.Core

type Shape =
    | Circle of radius: float
    | Empty

type Model = { Name: string; Shapes: Shape list }

let send (model: Model) : obj = Transfer.toPlain model

let receive (data: obj) : Model = Transfer.ofPlain data

let copy (model: Model) = Transfer.clone model

let rawCopy (values: string[]) = JS.structuredClone values
```

The conversion follows the static type, which the compiler passes as reflection info: `toPlain(model, Model_$reflection())`. In the plain form:

- a record or anonymous record is an object with its field names;
- a union case is `{ case: "Name", fields: [...] }`;
- a list, set, array or tuple is an array, and a map an array of `[key, value]` pairs;
- `None` is `null`, and `Some` is the value, or `{ value }` where the value is itself an option, `unit` or `obj`;
- a decimal is its string.

Numbers, strings, dates and class instances are kept as they are, so a class instance still loses its prototype in a clone. Where `'T` is a type parameter, call `Transfer` from an `inline` function so the compiler knows the type.

`JS.structuredClone` binds the global, and `JS.structuredCloneTransfer transfer value` moves objects such as an `ArrayBuffer` into the copy instead of copying them.

## Typed-array views

A numeric F# array is a typed array in JavaScript. An `ArraySegment` over one is a typed-array view that shares its memory: `ArraySegment<byte>(bytes, 4, 16)` compiles to `bytes.subarray(4, 20)`. `Array.ofArrayBuffer buffer` views a whole `ArrayBuffer` as a numeric array without a copy, and `array.AsArrayBuffer()` gives the array's buffer.

```fsharp fickl
open System
open Fable.Core
open Fickl.Core

let header (bytes: byte[]) = ArraySegment<byte>(bytes, 4, 16)

let floatsOf (buffer: JS.ArrayBuffer) : float[] = Array.ofArrayBuffer buffer

let bufferOf (bytes: byte[]) = bytes.AsArrayBuffer()
```

`Array.ofArrayBuffer` picks the typed array from the element type, `new Float64Array(buffer)` here. The buffer's length must be a multiple of the element size, and writes to either the buffer or the array show in the other. `AsArrayBuffer` returns the array's own buffer when the array covers all of it, as an array F# created does, and otherwise a copy of the part it covers. `JS.Constructors.BigUint64Array` binds the array `uint64[]` compiles to.

## Import attributes

`[<ImportWith("type", "json")>]` adds an import attribute to an imported value, function or class, for JSON and CSS modules. `JsInterop.importDynamicWith` does the same for a dynamic import, with the attributes as a list literal of key and value pairs.

```fsharp fickl
open Fable.Core
open Fickl.Core

type Config =
    abstract name: string
    abstract port: int

[<ImportDefault("./config.json"); ImportWith("type", "json")>]
let config: Config = jsNative

let address () = $"{config.name}:{config.port}"

let load () =
    JsInterop.importDynamicWith<{| ``default``: Config |}> "./config.json" [ "type", "json" ]
```

The static import compiles to `import config from "..." with { type: "json" }`, and the dynamic one to `import("./config.json", { with: { type: "json" } })`. The keys of `importDynamicWith` must be constants. A JSON or CSS module is the `default` export of the promised namespace object.

The static import's path is rewritten relative to the output folder, like any `[<Import>]`; the path of `importDynamicWith` is written as you give it, as with `importDynamic`. Each import may carry several `ImportWith` attributes, and the same path with other attributes is a separate declaration. A type that is only used as a type is imported with `import type`, which takes no attributes.

TypeScript needs `module` set to `esnext`, `preserve` or `nodenext` for import attributes.

---
title: Types
description: The Fickl.Core types and attributes that give your compiled F# precise TypeScript annotations.
---

Fickl.Core has types that exist mainly for their TypeScript annotation: property keys, plain-object records, branded primitives, the TypeScript utility types and `unknown`. Use them when TypeScript code calls your compiled F#, or when you bind a JavaScript API whose types F# cannot spell. Most of them compile to the bare value in JavaScript; only the annotations change.

## Property keys: `keyof`

`keyof` takes a lambda that reads one property and gives you the name JavaScript reads that property by. The result is a `keyof<'T, 'R>`: a key of `'T` whose value is an `'R`. `KeyOf.get` and `KeyOf.set` read and write the property through the key, and `key.Value` is the name as a string. A `keyof<'T>` is a key whose value type you do not track; `KeyOf.widen` converts one to the other.

```fsharp fickl
open Fickl.Core

type Server = { Port: int; Host: string; mutable Retries: int }

let port = keyof (fun (s: Server) -> s.Port)
let retries = keyof (fun (s: Server) -> s.Retries)

let read (key: keyof<Server, 'R>) (server: Server) : 'R = KeyOf.get key server
let name (key: keyof<Server>) = key.Value

let retry (server: Server) =
    KeyOf.set retries server (server.Retries + 1)
```

In JavaScript a key is the string itself, `const port = "Port"`, and `KeyOf.get` is `server[key]`. TypeScript annotates a `keyof<Server>` as `(keyof Server) & string` and a `keyof<Server, 'R>` as `KeysOfType<Server, R>`, the keys whose property is an `R`, so `target[key]` is checked. The name is the one the compiled access uses, so a renamed member gives its JavaScript name.

The lambda must read a single property of its argument. A path such as `_.a.b`, a method or a class property is a compile error. `KeyOf.tryGet` reads a property as an `obj option`, and `KeyOf.unsafeOfString` treats any string as a key, with a visible cast in TypeScript.

## Plain-object maps: `Record`, `ReadonlyRecord`, `PropertyRecord`

`Record<'K, 'V>` is a plain JavaScript object used as a map, typed `Record<K, V>` in TypeScript. `r.[key]` compiles to `r[key]`, and `r.[key] <- value` to `r[key] = value`. `Record.empty ()` builds `{}`, `Record.tryGet` finds only the object's own properties, so an inherited `toString` is not found, and `Record.keys` is `Object.keys`.

`ReadonlyRecord<'K, 'V>` is the same object without the setter, typed `Readonly<Record<K, V>>`. `PropertyRecord<'T, 'V>` has a `'V` for each property name of `'T`, typed `Record<keyof T & string, V>`, and you index it with a `keyof<'T>`.

```fsharp fickl
open Fickl.Core

let count (words: string[]) : Record<string, int> =
    let counts = Record.empty ()

    for word in words do
        counts.[word] <- (Record.tryGet word counts |> Option.defaultValue 0) + 1

    counts

let total (counts: ReadonlyRecord<string, int>) (word: string) = counts.[word]

type Theme = { Primary: string; Accent: string }

let primary (colors: PropertyRecord<Theme, string>) =
    colors.[KeyOf.widen (keyof (fun (t: Theme) -> t.Primary))]
```

TypeScript annotations write `globalThis.Record`, so the `Record` class of the Fable library cannot shadow the TypeScript type. A missing key reads as `undefined` at runtime, as it does in JavaScript; use `Record.tryGet` when the key may be absent.

## Property values: `valueof`

`valueof<'T>` is the value of some property of `'T`, without saying which: TypeScript's `T[keyof T]`. `ValueOf.ofKey key target` reads one, and `ValueOf.unsafeAs` treats it as a given type. Nothing checks that cast, and TypeScript shows it as an `as`.

```fsharp fickl
open Fickl.Core

type Server = { Port: int; Host: string }

let valueAt (key: keyof<Server, 'R>) (server: Server) : valueof<Server> =
    ValueOf.ofKey key server

let portOf (server: Server) : int =
    valueAt (keyof (fun (s: Server) -> s.Port)) server |> ValueOf.unsafeAs<Server, int>
```

The TypeScript signature of `valueAt` returns `Server[keyof Server]`. The JavaScript value is the property's value itself.

## Branded primitives: `string<'m>`, `bool<'m>`, `char<'m>`

F# already lets you tag a number with a unit of measure, `int<UserId>`. Fickl.Core adds the same for `string`, `bool` and `char`, so `string<UserId>` and `string<OrderId>` are different types in F# even though both are strings. `Brand.tag` and `Brand.untag` convert a string, and `Brand.tagBool`, `Brand.untagBool`, `Brand.tagChar` and `Brand.untagChar` convert the others.

```fsharp fickl
open Fickl.Core

[<Measure>]
type UserId

[<Measure>]
type OrderId

let userId (raw: string) : string<UserId> = Brand.tag raw

let describe (id: string<UserId>) = "user " + Brand.untag id

// describe (Brand.tag<OrderId> "o1") does not compile
```

The brand exists only for the F# type checker. The `Brand` functions compile to nothing, the value is the bare primitive in JavaScript, and TypeScript annotations write `string`, `boolean` and `string`.

## Utility types: `Partial`, `Required`, `Readonly`

`Partial<'T>`, `Required<'T>` and `Readonly<'T>` are TypeScript's utility types, for binding signatures such as `abstract setState: Partial<State> -> unit`. They change only the annotation; the JavaScript value is the object itself.

`partial` builds an object with some of a type's properties, from a lambda that sets them as `jsOptions` does. TypeScript checks the object literal with `satisfies Partial<T>`, so a misspelled key or a mistyped value is an error there. `full` treats a `Partial<'T>` as the complete `'T`, with a cast. Both live in `Fickl.Core.JsInterop`.

```fsharp fickl
open Fickl.Core
open Fickl.Core.JsInterop

type State =
    abstract count: int with get, set
    abstract name: string with get, set

let rename (name: string) : Partial<State> = partial (fun s -> s.name <- name)

let complete (patch: Partial<State>) : State = full patch

let snapshot (state: State) : Readonly<State> = unbox state
```

In TypeScript, `jsOptions` also checks its flat object literal with `satisfies Partial<T>` before casting it to the full type.

## `unknown`

`Fickl.Core.unknown` is `obj` in F#, so the two convert freely. A module function or abstract member that takes or returns `unknown` has `unknown` in its TypeScript signature, so TypeScript callers must narrow the value before they use it. `obj` stays `any`.

```fsharp fickl
open Fickl.Core

let parse (text: string) : unknown = Fable.Core.JS.JSON.parse text

let anything (value: obj) : obj = value

type IStore =
    abstract Get: key: string -> unknown
```

The function body still sees `any`: the TypeScript output declares `parse(text: string): unknown` and then an implementation signature returning `any`. Values, fields and properties typed `unknown` are annotated `any`. The JavaScript is unchanged.

## Your own annotation: `[<EmitType>]`

`[<EmitType>]` writes the TypeScript annotation of a type as the text you give, for types F# cannot describe: conditional, mapped and template literal types, or a library's helper type. `$0`, `$1`... in the text stand for the annotations of the type's generic arguments. With a second argument, a module path, the identifier the text starts with is imported from that module as a type.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<EmitType("`data-${string}`"); Erase>]
type DataAttr = DataAttr of string

[<EmitType("Record<string, $0>")>]
type Dict<'T> = interface end

[<EmitType("EventEmitter", "node:events")>]
type Emitter = interface end

let dataAttr (name: string) = DataAttr("data-" + name)
let lookup (dict: Dict<int>) (key: string) : int = unbox (JsInterop.emitJsExpr (dict, key) "$0[$1]")
let listen (emitter: Emitter) = emitter
```

The attribute changes only the TypeScript annotations; the JavaScript is the same with or without it. An interface or an erased union marked `[<EmitType>]` has no TypeScript declaration of its own, and a class keeps its JavaScript class. A relative module path is relative to the declaring F# file.

## Enums: `[<TsEnum>]` and imported `[<StringEnum>]` unions

Without an attribute, an F# enum emits nothing and is typed as its underlying number. `[<TsEnum>]` emits the enum as the frozen object a TypeScript enum compiles to, with its members and the reverse mapping, so JavaScript can read `Color.Red` and `Color[0]`. TypeScript types it with an ambient `declare enum`, which keeps the output erasable, and annotates values of the enum `Color` instead of `int32`.

A `[<StringEnum>]` union marked `[<Import>]` or `[<Global>]` binds a TypeScript string enum. Its cases compile to members of the imported object, `Direction.Up`, instead of their string literals, so they type-check against the enum.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<TsEnum>]
type Color =
    | Red = 0
    | Green = 1

let isRed (color: Color) = color = Color.Red

[<Import("Direction", "compass"); StringEnum>]
type Direction =
    | [<CompiledName("UP")>] Up
    | [<CompiledName("DOWN")>] Down

let opposite (dir: Direction) =
    match dir with
    | Up -> Down
    | Down -> Up
```

F# code is unchanged: values of a `[<TsEnum>]` enum stay plain numbers, so `isRed` compares with `0`. A `[<Flags>]` enum is typed `number`, as its values combine members. An enum with a 64-bit underlying type is not supported.

## Read-only collections

Fable annotates `IReadOnlyCollection<T>`, `IReadOnlyList<T>` and `IReadOnlyDictionary<K, V>` as `any`. Fickl annotates the first two `Iterable<T>` and the dictionary `IReadOnlyMap<K, V>`, so TypeScript can pass a `readonly T[]` or a `ReadonlyMap`.

For a parameter that must be an array, `Fickl.Core.JS.ReadonlyArray<'T>` binds TypeScript's `ReadonlyArray<T>`. It accepts a `readonly T[]` and an `as const` array, which a `'T[]` parameter rejects. `JS.ReadonlyArray.ofArray` and `JS.ReadonlyArray.ofResizeArray` view an F# array or `ResizeArray` without a copy, and `JS.ReadonlyArray.toArray` copies one back.

```fsharp fickl
open System.Collections.Generic
open Fickl.Core

let size (xs: IReadOnlyCollection<int>) = xs.Count
let first (xs: IReadOnlyList<string>) = xs.[0]
let find (d: IReadOnlyDictionary<string, int>) (key: string) = d.[key]

let sum (xs: JS.ReadonlyArray<int>) = Seq.sum xs
let total = sum (JS.ReadonlyArray.ofArray [| 1; 2; 3 |])
```

`ReadonlyArray` has only the members an F# array has whatever its representation. A numeric F# array is a typed array in JavaScript, which has no `concat` and no `map` to another element type, so use the `Seq` functions for those.

## Nullable reference types

With `<Nullable>enable</Nullable>` in your project, an F# 9 nullable reference type such as `string | null` is annotated `string | null` in TypeScript, so TypeScript code can pass and receive `null`. Fable erases the `| null` and writes `string`.

```fsharp fickl
let greet (name: string | null) =
    match name with
    | null -> "Hello"
    | name -> "Hello " + name

let tryFind (key: string) : string | null =
    if key = "" then null else key
```

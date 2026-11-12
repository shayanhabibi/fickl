---
title: Interfaces and unions
description: Attributes that shape how F# interfaces and erased unions look to JavaScript and TypeScript.
---

This page covers the Fickl.Core attributes for binding JavaScript shapes that an ordinary F# interface or union cannot describe: callable objects, augmented global interfaces, fluent `this`-returning methods, tagged unions over interfaces, erased cases told apart by a predicate, labelled tuples and TypeScript type guards. Use them when you write bindings, or when TypeScript code consumes your F# types.

## Callable objects: `[<CallableInterface>]`

Many JavaScript values are functions with properties: `debug` with `debug.enabled`, a React component with a `displayName`, Express middleware. Mark an interface `[<CallableInterface>]` and give it one `Invoke` method. An object expression implementing it compiles to a plain function, and `Invoke` calls compile to direct calls.

```fsharp fickl
open Fickl.Core

[<CallableInterface>]
type Middleware =
    abstract Invoke: request: string -> string
    abstract path: string

[<CallableInterface>]
type Identity =
    abstract Invoke<'T>: value: 'T -> 'T

let shout =
    { new Middleware with
        member _.Invoke request = request + "!"
        member _.path = "/" }

let identity = { new Identity with member _.Invoke value = value }

let handled = shout.Invoke "hello"
```

The other properties and methods of the interface are put on the function with `Object.assign`, and `shout.Invoke "hello"` is `shout("hello")`. A generic `Invoke` stays generic: TypeScript declares `Identity` as `<T>(value: T): T`.

The interface declares one `Invoke` method with one argument group, or inherits callable interfaces without declaring `Invoke`. When it inherits several, they must share the same instantiated interface that declares `Invoke`.

## Augmenting interfaces: `[<AugmentGlobal>]`, `[<AugmentModule>]`

`[<AugmentGlobal("Name")>]` adds the members of an F# interface to a global TypeScript interface, `declare global { interface Name { ... } }`. `[<AugmentModule("path", "Name")>]` adds them to an interface a module exports, `declare module "path" { ... }`. TypeScript code that imports your file then sees what your F# library contributes, for example `document.createElement("my-counter")` returning your custom element.

```fsharp fickl show=ts
open Fable.Core
open Fickl.Core

[<Global>]
type HTMLElement() =
    class
    end

type Counter() =
    inherit HTMLElement()
    member val count = 0 with get, set

[<AugmentGlobal("HTMLElementTagNameMap")>]
type TagNames =
    [<EmitProperty("my-counter")>]
    abstract counter: Counter

[<AugmentModule("vue", "ComponentCustomProperties")>]
type Translations =
    abstract translate: key: string -> string

let translateAll (plugin: Translations) (keys: string list) =
    keys |> List.map plugin.translate
```

The interface has no declaration of its own, and annotations of it are the augmented interface: `translateAll` takes a `ComponentCustomProperties` imported from `"vue"`. A generic interface must have the same type parameters as the interface it augments. A relative module path is relative to the declaring F# file. Only the TypeScript output and declaration files change; an interface has no JavaScript.

## Fluent methods: `[<ReturnsThis>]` and `Fluent.chain`

A query builder's `where` returns the builder, and in TypeScript a subinterface keeps its own members through the chain because `where` returns `this`. Mark such an abstract method `[<ReturnsThis>]` and TypeScript declares it returning `this`, `where(arg0: string): this`. The method must return its declaring interface or abstract class, or one of its bases.

F# still types `select.where "x"` as the declaring interface, `IQuery`. `Fluent.chain` makes the same call and types the result as the receiver, so the subinterface's members stay in reach.

```fsharp fickl
open Fickl.Core

type IQuery =
    [<ReturnsThis>]
    abstract where: string -> IQuery

type ISelect =
    inherit IQuery
    abstract columns: string -> ISelect

let build (select: ISelect) =
    select
    |> Fluent.chain (fun s -> s.where "active")
    |> fun s -> s.columns "name"
```

The JavaScript is unchanged. F# classes and object expressions implementing the method return `this` too, with an `as this` cast in TypeScript: the attribute promises that the method returns its receiver, or an object of the receiver's type. `Fluent.chain` casts its result, which fails on .NET if the member returned another type.

## Tagged unions over interfaces: `[<InlineInterface>]`

A `[<TypeScriptTaggedUnion>]` case normally holds its fields in an object next to the tag. `[<InlineInterface>]` on a case with exactly one field, whose type is an interface, compiles the case as that interface with the tag added: TypeScript types it `Take & { type: "take" }`. On the union type, it applies to every case that has fields.

```fsharp fickl
open Fable.Core
open Fickl.Core

type Take =
    abstract fromIndex: int
    abstract toIndex: int

[<RequireQualifiedAccess; TypeScriptTaggedUnion("type")>]
type Command =
    | [<InlineInterface>] Take of Take
    | Save

let take (range: Take) = Command.Take range

let length (command: Command) =
    match command with
    | Command.Take t -> t.toIndex - t.fromIndex
    | Command.Save -> 0
```

When the field value already carries the case's tag, the case is the value itself. Otherwise the tag is added without changing the value, and the `InlineInterfaceRepresentation` argument chooses how:

- `View`, the default, is a tagged view that forwards to the value, so JavaScript sees its current state. It cannot be structured-cloned or frozen.
- `Copy` copies the value's own enumerable properties, with its prototype and the tag, into an ordinary object. Later writes do not reach the original.
- `CopyPlainObjects` copies a plain object and makes a view of anything else.

An argument on a case overrides one on the union type. Reading the field, `Command.Take t`, gives back the original object.

## Telling erased cases apart: `[<CaseTest>]`

An `[<Erase>]` union is matched by the runtime type of its payload, so two cases holding a string cannot be told apart. `[<CaseTest>]` gives a case a JavaScript predicate in `Emit` syntax, where `$0` is the tested value.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<RequireQualifiedAccess; Erase>]
type Reference =
    | [<CaseTest("$0.startsWith('n:')")>] Name of string
    | Slug of string

let describe (reference: Reference) =
    match reference with
    | Reference.Name name -> "name " + name
    | Reference.Slug slug -> "slug " + slug
```

The predicate replaces the payload type test of its case. Construction and extraction still erase to the payload: no tag is added and no copy is made, so the predicate must recognise the values of its case. Cases without the attribute keep their ordinary type tests.

On a `[<TypeScriptTaggedUnion>]`, `[<CaseTest>]` is an extra test checked after the tag matches, for cases that share a tag through the same `CompiledName`. `Fable.Core.CaseTest` keeps the payload type test when one exists; `Fickl.Core.CaseTest` replaces it.

## Labelled and optional tuple elements

A single-case `[<Erase>]` union with several fields compiles to an array. When the fields are named, TypeScript annotates it as a labelled tuple, `[key: string, value: int32]`, and trailing option fields become optional elements.

```fsharp fickl
open Fable.Core

type Init =
    abstract method: string

[<Erase>]
type FetchArgs = FetchArgs of url: string * init: Init option

let index = FetchArgs("/", None)
let send (init: Init) = FetchArgs("/submit", Some init)

let url (FetchArgs(url, _)) = url
```

The type is `[url: string, init?: Option<Init>]`, and a trailing `None` is left out of the array, so `FetchArgs("/", None)` is `["/"]` and TypeScript can pass the shorter tuple to a function such as `fetch`.

## Type guards: `[<TypePredicate>]`, `[<TypeAssertion>]`

`[<TypePredicate("x", typeof<User>)>]` emits a function as a TypeScript type predicate, `x is User`, and `[<TypeAssertion("x", typeof<User>)>]` as an assertion function, `asserts x is User`. The first argument names the narrowed parameter.

F# can see the guard as returning the narrowed value: a predicate returns `bool`, `User option` or `User voption`, and an assertion returns `unit` or `Result<User, exn>`. The compiled function still returns a boolean, or returns nothing and throws the exception of `Error`, and F# calls get `Some x` or `Ok x` back.

```fsharp fickl
open Fickl.Core

type User = { Name: string }

[<TypePredicate("value", typeof<User>)>]
let tryUser (value: obj) : User option =
    match value with
    | :? User as user -> Some user
    | _ -> None

[<TypeAssertion("value", typeof<User>)>]
let checkUser (value: obj) : Result<User, exn> =
    match value with
    | :? User as user -> Ok user
    | _ -> Error(exn "not a user")

let greet (value: obj) =
    match tryUser value with
    | Some user -> "Hello " + user.Name
    | None -> "Hello"
```

The attributes apply to module functions and non-abstract class methods. They also bind an imported guard: put `[<TypePredicate>]` next to `[<Import>]` on a `jsNative` function, such as axios's `isAxiosError`.

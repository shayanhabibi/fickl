---
title: Explicit expression control
description: Attributes and functions that decide the exact JavaScript shape of a function, a binding or an emitted expression.
---

Fickl compiles idiomatic F# to idiomatic JavaScript: a function of two arguments is a JavaScript function of two parameters, a lambda is an arrow function, and a binding used once is inlined. This page covers the opt-outs, for the places where JavaScript expects a different shape: curried callbacks, functions with their own `this`, bindings that must stay where you wrote them, raw code in `[<Emit>]` templates and native tagged templates.

## Curried functions: `[<Curried>]`

Fickl passes a function of several arguments as one JavaScript function that takes all of them, `(a, b) => ...`, and calls it as `f(a, b)`. A JavaScript API that calls its callback as `f(a)(b)`, or JavaScript code that hands F# a curried function, needs the curried shape instead. Mark the parameter `[<Curried>]`.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<Import("subscribe", "events-lib")>]
let subscribe ([<Curried>] handler: string -> int -> unit) : unit = jsNative

let apply ([<Curried>] f: int -> int -> int) = f 1 2
let applyTupled (f: int -> int -> int) = f 1 2

subscribe (fun name code -> JS.console.log (name, code))
```

The parameter is curried both where the function is called and inside its body: `apply` calls `f(1)(2)`, while `applyTupled` calls `f(1, 2)`. On an abstract member, the attribute also applies to every implementation of it.

On a record field or a union case field, `[<Curried>]` keeps the function stored in the field curried, for a JavaScript object that stores or calls it one argument at a time.

```fsharp fickl
open Fickl.Core

type Handlers =
    { [<Curried>] onKey: string -> int -> string
      onClick: int -> int -> string }

let handlers =
    { onKey = fun key code -> key + string code
      onClick = fun x y -> string (x + y) }

let press (h: Handlers) = h.onKey "a" 1 + h.onClick 2 3
```

## Functions with their own `this`: `jsFunction` and `[<JsFunction>]`

A lambda compiles to an arrow function, which has no `this` or `arguments` of its own. `jsFunction` compiles a lambda to an ordinary `function` instead, for APIs that call the callback with a receiver. `[<JsFunction>]` on a parameter does the same for every lambda written at a call to it.

```fsharp fickl
open Fable.Core
open Fickl.Core
open Fickl.Core.JsInterop

let handler: obj -> obj = jsFunction (fun (event: obj) -> Fable.Core.JsInterop.emitJsExpr event "this.handle($0)")

[<Emit("$0.addEventListener('click', $1)")>]
let onClick (target: obj) ([<JsFunction>] listener: obj -> unit) : unit = jsNative

let wire (button: obj) = onClick button (fun _ -> printfn "clicked")
```

`jsFunction` takes a lambda written at the call; a reference to an existing function is rejected. In TypeScript the function has a `this: any` parameter.

## A typed receiver: `withThis`

`withThis` compiles a lambda to a `function` whose first argument is its `this` receiver. TypeScript types the receiver as a `this` parameter, and the body reads it as an ordinary F# value.

```fsharp fickl
open Fable.Core
open Fickl.Core.JsInterop

type Button =
    abstract id: string

[<Emit("$0.addEventListener('click', $1)")>]
let onClick (target: Button) (listener: obj -> unit) : unit = jsNative

let wire (button: Button) =
    onClick button (withThis (fun (self: Button) (_event: obj) -> JS.console.log self.id))
```

The F# type of the result drops the receiver, `obj -> unit` here. Call it through a JavaScript API that supplies a receiver: an ordinary F# call supplies none.

## Keeping a binding: `JsInterop.keepBinding`

A release build replaces a `let` binding that is used once by its value, where that keeps the order of evaluation. The calls still run in the same order, but they move into the expression that uses them. For code whose shape matters, such as React hooks under the rules of hooks, wrap the value in `keepBinding`.

```fsharp fickl
open Fickl.Core.JsInterop

let useCtx () = 1
let useScroll (threshold: int, scrollRef: int) = threshold > scrollRef

let inlined threshold =
    let scrollRef = useCtx ()
    let isThreshold = useScroll (threshold, scrollRef)
    isThreshold && threshold > 0

let kept threshold =
    let scrollRef = keepBinding (useCtx ())
    let isThreshold = keepBinding (useScroll (threshold, scrollRef))
    isThreshold && threshold > 0
```

In `inlined`, both calls move into the condition, `if (useScroll(threshold, useCtx()))`. In `kept`, each stays a `const`. `keepBinding` has an effect only as the whole value of a `let`; anywhere else, and on .NET, it is the identity function.

## Raw code in `[<Emit>]` templates: `$N!`

In an `[<Emit>]` template, `$0`, `$1`... are the arguments as expressions. `$N!` writes argument `N`, which must be a string literal, as raw code instead of as a string. Use it to emit a name or a keyword that the caller chooses, as in `const $0! = $1`.

```fsharp fickl
open Fable.Core

[<Emit("const $0! = $1")>]
let declare (name: string) (value: obj) : unit = jsNative

declare "answer" 42
```

When argument `N` is anything other than a string literal, Fable prints nothing for it; Fickl reports an error instead, as there is no code to splice.

For a TypeScript non-null assertion on an argument, write `($N)!`, with the parentheses. The `!` is copied to the JavaScript output too, where it is not valid syntax, so use it only in code you compile to TypeScript.

```fsharp fickl show=ts
open Fable.Core

[<Emit("($0)!.length")>]
let lengthOf (text: string | null) : int = jsNative

let size (text: string | null) = lengthOf text
```

## Tagged templates: `[<TaggedTemplate>]`

`[<TaggedTemplate>]` on an imported, global or emitted function that takes one `FormattableString` calls it with an F# interpolated string as a native tagged template. ``html $"<p>{name}</p>"`` compiles to ``html`<p>${name}</p>` ``, for Lit, GraphQL and SQL tags and for `String.raw`.

```fsharp fickl
open System
open Fable.Core
open Fickl.Core

type TemplateResult = interface end

[<Import("html", "lit"); TaggedTemplate>]
let html (template: FormattableString) : TemplateResult = jsNative

[<Emit("String.raw"); TaggedTemplate>]
let raw (template: FormattableString) : string = jsNative

let view (name: string) (count: int) = html $"<p>Hello {name}, {count} items</p>"

let path (dir: string) = raw $"/{dir}/file"
```

The tag gets the frozen strings array, the same object on every evaluation, and the values unformatted. A format specifier such as `{x:N2}` is a compile error. An emitted tag must be an expression without placeholders, such as `String.raw`. A `FormattableString` that is not written at the call is passed as its strings and arguments.

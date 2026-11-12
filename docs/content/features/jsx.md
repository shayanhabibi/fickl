---
title: JSX
description: Write JSX components in F# and pass them props the compiler can read.
---

This page covers how F# compiles to JSX: components marked `[<JSX.Component>]`, plain functions rendered as components with `JSX.el`, and the props list of `JSX.create`, which the compiler has to read while compiling. You need it when you write React or another JSX framework in F#, and when the compiler rejects a props list.

The output is JSX syntax, not function calls, so a JSX tool such as Babel, esbuild or the TypeScript compiler has to process it. Give the files an extension that tool recognises, such as `.jsx` or `.tsx`, with `-e` (`--extension`).

## Components

`[<JSX.Component>]` turns a function into a component: its arguments become the props, except a `children` argument. `JSX.html` builds an element from an interpolated string template, whose holes must follow JSX syntax.

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

#nowarn "3884" // A function value in an interpolated string

[<JSX.Component>]
let Greeting (name: string) (count: int) =
    JSX.html $"""<p>Hello {name}, you have {count} messages</p>"""

let Route: obj = importMember "router-lib"

let page () =
    JSX.html $"""<main>{Greeting "Ada" 3}<Route component={Greeting} /></main>"""
```

Calling a component in a JSX position renders it as an element: `{Greeting "Ada" 3}` compiles to `<Greeting name="Ada" count={3} />`. A component used as a value in a template hole, as a prop of `JSX.create` or as its tag is passed by reference, `component={Greeting}`, so the framework sees the same component on every render and does not remount its subtree ([#310](https://github.com/shayanhabibi/fickl/issues/310)). A partial application, or a lambda that calls the component, keeps its arrow function.

F# warns about a function value in an interpolated string (FS3884), because outside JSX it would be formatted with `ToString`. In a JSX template it is the component reference, so the example turns the warning off.

## Rendering a plain function with `JSX.el`

A function that returns `JSX.Element` without `[<JSX.Component>]` is called in place, `{Row(row)}`, so it is not a component of its own: it shares the hooks state of its caller and has no node in the devtools. `JSX.el Row props` renders it as a tag instead, without changing how the function is compiled ([#308](https://github.com/shayanhabibi/fickl/issues/308)).

```fsharp fickl
open Fable.Core

type RowProps = {| name: string; count: int |}

let Row (props: RowProps) =
    JSX.html $"<li>{props.name}: {props.count}</li>"

let list (row: RowProps) =
    JSX.html
        $"""<ul>
            {Row row}
            {JSX.el Row row}
        </ul>"""
```

`{JSX.el Row row}` compiles to `<Row {...row} />`. The function must already have the shape of a component: a name starting with an uppercase letter, taking either `unit` or one props object, an anonymous record, an interface, a `[<JS.Pojo>]` class or `obj`. A record, a tuple, a lambda written in place, a decorated component or a lowercase name is a compile error. Declare the function in a module or a type: a function created during a render is a new component every time and loses its state.

## The props list of `JSX.create`

`JSX.create tag props` compiles to JSX syntax, `<div className="box" />`. JSX has no way to take its attributes from a list built at run time, so the compiler reads the props list while compiling: it has to see each entry, and the key of each entry, in the source. The list must be a list literal written at the call, or passed through `inline` functions down to the call.

### A key and a value

The key must be a string literal. The value can be any expression. An `inline` function that returns the tuple also works, because the key is visible once the function is inlined.

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

let literal (name: string) =
    JSX.create "div" [ "className" ==> "box"; "title" ==> name ]

type prop =
    static member inline title(value: string) : JSX.Prop = "title", value

let inlineHelper (name: string) = JSX.create "div" [ prop.title name ]
```

### `children`

The `children` entry is taken out of the attributes and printed between the tags. Its value is one element or a list of elements, and that list can use `if`, `match`, `for` and `yield!` freely.

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

let oneChild (name: string) =
    JSX.create "div" [ "children" ==> JSX.text name ]
```

### A condition

An entry can be under `if`, with or without `else` and `elif`. JSX cannot leave out an attribute conditionally, so the props of a condition are emitted as an object that is spread into the element.

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

let conditional (isActive: bool) =
    JSX.create
        "div"
        [
            "className" ==> "box"
            if isActive then
                "title" ==> "active"
        ]

let conditionalElse (isActive: bool) =
    JSX.create
        "div"
        [
            if isActive then
                "title" ==> "active"
                "tabIndex" ==> 0
            else
                "title" ==> "inactive"
        ]
```

Props keep the order they are written in, so a later prop overrides an earlier one with the same key, as in JSX.

### `yield!` of a list literal

The entries of the inner list are read as if they were written in the outer one.

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

let yieldBang (name: string) =
    JSX.create "div" [ "className" ==> "box"; yield! [ "title" ==> name; "id" ==> "main" ] ]
```

### `JSX.spread`

`JSX.spread` spreads an object built at run time into the element, `{...rest}`. It is the way to pass props the compiler cannot read. Under a condition it is merged with the other props of that condition.

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

let spread (rest: obj) =
    JSX.create "div" [ "className" ==> "box"; JSX.spread rest; "title" ==> "wins" ]

let conditionalSpread (isActive: bool) (rest: obj) =
    JSX.create
        "div"
        [
            if isActive then
                "title" ==> "active"
                JSX.spread rest
        ]
```

`JSX.spread` has no value of its own: it only marks where an object is spread. It can only be an element of the props list passed to `JSX.create`, so it cannot be returned from a function that is not `inline`, or put in any other list.

## When the compiler cannot read the list

When the compiler cannot read a props list, compilation fails with one of the errors below. The compiler's message links to this explanation.

### Expecting a static list or array literal (no generator) for JSX props

The props argument is a value built elsewhere, or the list contains `for`, `let`, a `match` that is more than a two-way test, or a `yield!` of something other than a list literal. Each of these fails:

```fsharp
let variable (props: JSX.Prop list) = JSX.create "div" props

let yieldVariable (rest: JSX.Prop list) =
    JSX.create "div" [ "className" ==> "box"; yield! rest ]

let forInList (names: string list) =
    JSX.create "div" [ for name in names -> $"data-{name}" ==> true ]

let match3 (size: int) =
    JSX.create
        "div"
        [
            match size with
            | 0 -> "className" ==> "small"
            | 1 -> "className" ==> "medium"
            | _ -> "className" ==> "large"
        ]
```

Props that are only known at run time go into an object, which `JSX.spread` spreads into the element; `createObj` builds one from a list of any shape. When a `match` or a `let` only chooses a value, move it out of the list and keep the entry. When it chooses whether the entry is there at all, write it as `if`.

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

let fixFor (names: string list) =
    let props = createObj [ for name in names -> $"data-{name}" ==> true ]
    JSX.create "div" [ JSX.spread props ]

let fixMatch (size: int) =
    let className =
        match size with
        | 0 -> "small"
        | 1 -> "medium"
        | _ -> "large"

    JSX.create "div" [ "className" ==> className ]

let fixOption (title: string option) =
    JSX.create
        "div"
        [
            if title.IsSome then
                "title" ==> title.Value
        ]
```

A function that takes the props list and passes it to `JSX.create` must be `inline`, so that the literal written by its caller reaches the call.

### Cannot detect JSX prop key at compile time

An entry of the list is not a tuple whose first item is a string literal: the key is a variable, or the entry comes from a function that is not `inline`.

```fsharp
let dynamicKey (key: string) (value: string) =
    JSX.create "div" [ key ==> value ]

let title (value: string) : JSX.Prop = "title", value

let helper (name: string) = JSX.create "div" [ title name ]
```

Mark the function `inline`, as in [a key and a value](#a-key-and-a-value). A key that is only known at run time goes through an object, as `fixKey` below shows.

### JSX children cannot be set under a condition in the props list

The props of a condition are spread into the element as an object, and children are printed between the tags, which an object cannot do. This fails:

```fsharp
let conditionalChildren (show: bool) (name: string) =
    JSX.create
        "div"
        [
            "className" ==> "box"
            if show then
                "children" ==> JSX.text name
        ]
```

Keep `children` at the top of the list and put the condition inside its value, as `fixChildren` does:

```fsharp fickl
open Fable.Core
open Fable.Core.JsInterop

let fixKey (key: string) (value: string) =
    JSX.create "div" [ JSX.spread (createObj [ key ==> value ]) ]

let fixChildren (show: bool) (name: string) =
    JSX.create "div" [ "className" ==> "box"; "children" ==> [ if show then JSX.text name ] ]
```

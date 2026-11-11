# The props list of `JSX.create`

`JSX.create tag props` compiles to JSX syntax, `<div className="box" />`, not to a function call. JSX has no way to take its attributes from a list built at run time, so the compiler reads the props list while compiling: it has to see each entry, and the key of each entry, in the source. This applies to the JavaScript and TypeScript targets.

When it cannot, compilation fails with one of the [errors](#errors) below, each of which links here.

The examples assume:

```fsharp
open Fable.Core
open Fable.Core.JsInterop
```

## What the list can contain

The list must be a list literal written at the call, or passed through `inline` functions down to the call. Its entries can be the following.

### A key and a value

The key must be a string literal. The value can be any expression.

```fsharp
let literal (name: string) =
    JSX.create "div" [ "className" ==> "box"; "title" ==> name ]
```

```jsx
<div className="box"
    title={name} />
```

An `inline` function that returns the tuple also works, because the key is visible once the function is inlined:

```fsharp
type prop =
    static member inline title(value: string) : JSX.Prop = "title", value

let inlineHelper (name: string) = JSX.create "div" [ prop.title name ]
```

```jsx
<div title={name} />
```

### `children`

The `children` entry is taken out of the attributes and printed between the tags. Its value is one element or a list of elements, and that list can use `if`, `match`, `for` and `yield!` freely:

```fsharp
let oneChild (name: string) =
    JSX.create "div" [ "children" ==> JSX.text name ]
```

```jsx
<div>
    {name}
</div>
```

### A condition

An entry can be under `if`, with or without `else` and `elif`. JSX cannot leave out an attribute conditionally, so the props of a condition are emitted as an object that is spread into the element:

```fsharp
let conditional (isActive: bool) =
    JSX.create
        "div"
        [
            "className" ==> "box"
            if isActive then
                "title" ==> "active"
        ]
```

```jsx
<div className="box"
    {...isActive ? {
        title: "active",
    } : {}} />
```

```fsharp
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

```jsx
<div {...isActive ? {
        title: "active",
        tabIndex: 0,
    } : {
        title: "inactive",
    }} />
```

Props keep the order they are written in, so a later prop overrides an earlier one with the same key, as in JSX.

### `yield!` of a list literal

The entries of the inner list are read as if they were written in the outer one:

```fsharp
let yieldBang (name: string) =
    JSX.create "div" [ "className" ==> "box"; yield! [ "title" ==> name; "id" ==> "main" ] ]
```

```jsx
<div className="box"
    title={name}
    id="main" />
```

### `JSX.spread`

`JSX.spread` spreads an object built at run time into the element. It is the way to pass props the compiler cannot read:

```fsharp
let spread (rest: obj) =
    JSX.create "div" [ "className" ==> "box"; JSX.spread rest; "title" ==> "wins" ]
```

```jsx
<div className="box"
    {...rest}
    title="wins" />
```

Under a condition it is merged with the other props of that condition:

```fsharp
let conditionalSpread (isActive: bool) (rest: obj) =
    JSX.create
        "div"
        [
            if isActive then
                "title" ==> "active"
                JSX.spread rest
        ]
```

```jsx
<div {...isActive ? Object.assign({}, {
        title: "active",
    }, rest) : {}} />
```

## Errors

### Expecting a static list or array literal (no generator) for JSX props

The props argument is not a list the compiler can read: it is a value built elsewhere, or the list contains `for`, `let`, a `match` that is more than a two-way test, or a `yield!` of something other than a list literal.

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

Props that are only known at run time go into an object, which `JSX.spread` spreads into the element. `createObj` builds one from a list of any shape:

```fsharp
let fixVariable (props: obj) = JSX.create "div" [ JSX.spread props ]

let fixYield (rest: JSX.Prop list) =
    JSX.create "div" [ "className" ==> "box"; JSX.spread (createObj rest) ]

let fixFor (names: string list) =
    let props = createObj [ for name in names -> $"data-{name}" ==> true ]
    JSX.create "div" [ JSX.spread props ]
```

```jsx
<div {...props} />

<div className="box"
    {...createObj(rest)} />

<div {...createObj(toList(delay(() => map((name) => [concat("data-", name), true], names))))} />
```

When a `match` or a `let` only chooses a value, move it out of the list and keep the entry:

```fsharp
let fixMatch (size: int) =
    let className =
        match size with
        | 0 -> "small"
        | 1 -> "medium"
        | _ -> "large"

    JSX.create "div" [ "className" ==> className ]
```

```jsx
<div className={(size === 0) ? "small" : ((size === 1) ? "medium" : "large")} />
```

When it chooses whether the entry is there at all, write it as `if`:

```fsharp
let fixOption (title: string option) =
    JSX.create
        "div"
        [
            if title.IsSome then
                "title" ==> title.Value
        ]
```

```jsx
<div {...(title != null) ? {
        title: value_1(title),
    } : {}} />
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

Mark the function `inline` (see [a key and a value](#a-key-and-a-value)). A key that is only known at run time goes through an object:

```fsharp
let fixKey (key: string) (value: string) =
    JSX.create "div" [ JSX.spread (createObj [ key ==> value ]) ]
```

```jsx
<div {...createObj(singleton_1([key, value]))} />
```

### JSX children cannot be set under a condition in the props list

The props of a condition are spread into the element as an object, and children are printed between the tags, which an object cannot do.

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

Keep `children` at the top of the list and put the condition inside its value:

```fsharp
let fixChildren (show: bool) (name: string) =
    JSX.create "div" [ "className" ==> "box"; "children" ==> [ if show then JSX.text name ] ]
```

```jsx
<div className="box">
    {show ? (name) : (null)}
</div>
```

### JSX.spread can only be used as an element of the props list passed to JSX.create

`JSX.spread` has no value of its own: it only marks where an object is spread, so it cannot be returned from a function that is not `inline`, or put in any other list.

```fsharp
let spreadOutside (rest: obj) : JSX.Prop = JSX.spread rest
```

Write it in the props list, or make the function `inline`.

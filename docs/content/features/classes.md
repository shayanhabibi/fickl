---
title: Classes
description: How Fickl compiles F# classes that JavaScript extends, decorates, constructs or calls by name.
---

This page covers F# classes that JavaScript code sees directly: subclasses of JavaScript classes such as custom elements, ES `#private` fields, native decorators, symbol-keyed members, `new.target`, and predictable method names. You need it when a platform or a library calls into your class, rather than only your own F# code.

## Subclasses of JavaScript classes

By default, Fable compiles the members of an F# class to module functions next to the class, `Counter__Increment(counter)`, and only attaches them to the class with `[<AttachMembers>]`. That breaks a class that the platform calls into, such as a custom element: the browser looks for `connectedCallback` and `static observedAttributes` on the class itself.

In Fickl, a class that inherits a global, imported or `[<AttachMembers>]` class attaches its members without an attribute. Methods, properties and static members become members of the JavaScript class.

```fsharp fickl
open Fable.Core

[<Global>]
type HTMLElement() =
    member _.setAttribute(name: string, value: string) : unit = jsNative

type CounterElement() =
    inherit HTMLElement()
    let mutable count = 0

    static member observedAttributes = [| "count" |]

    member this.connectedCallback() =
        this.setAttribute("role", "status")

    member _.attributeChangedCallback(name: string, _old: string, value: string) =
        if name = "count" then count <- int value

    member _.Count = count
```

The `let` field `count` is also an ES `#private` field, `this.#count`, so it cannot set or shadow a property of the base class. See the next section.

To keep the members of such a class as module functions, mark it `[<Fickl.Core.AttachMembers(false)>]`. `[<Fickl.Core.AttachMembers>]` and `[<Fickl.Core.AttachMembers(true)>]` are the same as Fable.Core's attribute. As with Fable's attribute, overloads of attached members do not work.

```fsharp fickl
open Fable.Core

[<Global>]
type EventTarget() =
    class
    end

[<Fickl.Core.AttachMembers(false)>]
type Detached() =
    inherit EventTarget()
    member _.Value = 5
```

## Private fields: `[<PrivateFields>]` and `--privateFields`

Fable compiles the `let` fields of a class to ordinary properties. In a subclass of `HTMLElement`, a field named `title` or `hidden` then sets the element's own property, and in a subclass of `Map` a field named `size` fails, as `Map.size` has only a getter. `[<PrivateFields>]` compiles the class's `let` fields to ES `#private` fields, `this.#balance`, which cannot collide with a base class property and which JavaScript cannot read or enumerate.

```fsharp fickl
open Fickl.Core

[<PrivateFields>]
type Account(owner: string) =
    let mutable balance = 0.0
    member _.Owner = owner
    member _.Balance = balance
    member _.Deposit(amount: float) = balance <- balance + amount
```

The members of the class are attached, because only the class body can read `#private` fields. A class that inherits a global or imported JavaScript class uses private fields without the attribute, and `[<PrivateFields(false)>]` turns them off.

The `--privateFields` flag does the same for every class with attached members, without marking each one.

```fsharp fickl flags="--privateFields"
open Fable.Core

[<AttachMembers>]
type Account(opening: float) =
    let mutable balance = opening
    member _.Balance = balance
    member _.Deposit(amount: float) = balance <- balance + amount
```

See [--privateFields](../flags/index.md#privatefields) for the flag on its own.

## Decorators: `[<JsDecorator>]`

`[<JsDecorator>]` emits a native JavaScript decorator on a class, a method, a property or a field. The first argument is the decorator expression. With a second argument, a module path, the identifier the expression starts with is imported from that module; without one, the expression is a global. You can stack several on one member.

```fsharp fickl
open Fickl.Core

[<JsDecorator("customElement('todo-list')", "lit/decorators.js")>]
type TodoList() =
    [<JsDecorator("property()", "lit/decorators.js")>]
    member val Title = "Todo" with get, set

    [<JsDecorator("eventOptions({ passive: true })", "lit/decorators.js")>]
    member _.OnScroll(_event: obj) = ()
```

A decorated auto-property compiles to an `accessor`. Pass `true` as the last argument, `JsDecorator("expr", true)` or `JsDecorator("expr", "module", true)`, to decorate a native field instead. A relative import path is relative to the declaring F# file. The decorators run in the tool that executes the output, so it must support standard decorators.

## Symbol keys: `[<SymbolKey>]`

`[<SymbolKey>]` uses a symbol expression as the JavaScript key of a member, an interface slot or a record field, instead of its name. Use a well-known symbol such as `Symbol.iterator` or `Symbol.toPrimitive`, a stable global such as `Symbol.for('app.state')`, or an imported symbol, with the module path as the second argument.

```fsharp fickl
open Fickl.Core

type ILabel =
    [<SymbolKey("Symbol.toStringTag")>]
    abstract Label: string

let create (label: string) : ILabel =
    { new ILabel with member _.Label = label }

type Money(cents: int) =
    [<SymbolKey("Symbol.toPrimitive")>]
    member _.ToPrimitive(_hint: string) = cents
```

A class member with a symbol key is attached to the class without `[<AttachMembers>]`, so `+money` calls `Money`'s `[Symbol.toPrimitive]`. The expression must give the same symbol at every access; `Symbol('x')` creates a new one each time.

## `new.target`: `JS.newTarget` and `JS.isNewTarget`

`JS.newTarget` is JavaScript's `new.target`: the class being constructed, which is the subclass when a subclass constructor runs. `JS.isNewTarget<'T>` tests whether that class is `'T` itself, `new.target === Shape`, which makes an abstract-base guard.

```fsharp fickl
open Fickl.Core

type Shape() =
    do
        if JS.isNewTarget<Shape> then
            failwith "Shape is abstract"

type Square() =
    inherit Shape()

type Base() =
    let created: obj = JS.newTarget
    member _.Created = created
```

Both are allowed only in a primary constructor: its `let` and `do` bindings and the arguments of its `inherit` call. Inside a lambda or an object expression there, and anywhere else, they are a compile error. For `JS.isNewTarget`, `'T` must be a class that compiles to a JavaScript class. On .NET both throw.

## Predictable method names: `[<NoOverloadSuffix>]`

JavaScript has no overloading, so Fable names a class method after its type and adds a hash of its parameter types, `Counter__Subtract_Z524259A4`, even when the method is not overloaded. `[<NoOverloadSuffix>]` drops the hash, so JavaScript can call the method by a name you can predict, `Counter__Add`.

```fsharp fickl
open Fickl.Core

type Counter(start: int) =
    [<NoOverloadSuffix>]
    member _.Add(x: int) = start + x

    member _.Subtract(x: int) = start - x
```

On a type, the attribute drops the hash of every member that is not overloaded; overloaded members keep theirs. On an overloaded member it is an error. On a property, it applies to the getter and the setter.

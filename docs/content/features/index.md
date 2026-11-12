---
title: Features
description: What Fickl adds to Fable for JavaScript and TypeScript, by area.
---

Everything on these pages is in the `Fickl.Core` package or behind a compiler flag, and targets JavaScript and TypeScript. Each example is compiled by Fickl when the site is built: the tabs show the F# and the JavaScript and TypeScript it compiled to.

## Interop

::::cards
:::card title="Types" href="/fickl/features/types/"
`keyof`, `Record`, `valueof`, `Partial`, branded primitives, `unknown`, `[<EmitType>]` and TypeScript enums.
:::
:::card title="Interfaces" href="/fickl/features/interfaces/"
Callable interfaces, global and module augmentations, `this` return types, type guards and labelled tuples.
:::
:::card title="Classes" href="/fickl/features/classes/"
Subclasses of JavaScript classes, custom elements, `#private` fields, decorators and symbol keys.
:::
:::card title="Explicit control" href="/fickl/features/emit/"
Keep a function curried, write a real `function`, keep a binding, emit templates and tagged templates.
:::
:::card title="JSX" href="/fickl/features/jsx/"
Components and their props.
:::
::::

## Runtime

::::cards
:::card title="Async and promises" href="/fickl/features/async/"
`jsAsync` and `promise` as native `async` functions, cancellation and top-level `await`.
:::
:::card title="Generators and disposal" href="/fickl/features/generators/"
Native generators and async generators, and `use` with `Symbol.dispose`.
:::
:::card title="Errors" href="/fickl/features/errors/"
Catch JavaScript errors by type, and make F# exceptions real `Error`s.
:::
:::card title="Runtime bindings" href="/fickl/features/runtime/"
Events, `WeakRef`, `Proxy`, `structuredClone`, typed-array views and import attributes.
:::
:::card title="Reflection" href="/fickl/features/reflection/"
`GetMethod`, constructors and union case attributes in JavaScript.
:::
::::

---
title: What is Fickl
description: A fork of the Fable compiler that puts the JavaScript and TypeScript experience first.
---

Fickl is a fork of [Fable](https://fable.io), the F# to JavaScript compiler. It compiles the same projects with the same attributes, and spends its effort on JavaScript and TypeScript instead of on more target languages.

## Priorities

In order:

1. The performance and ergonomics of the compiler.
2. Explicit control of the code it emits.
3. The performance and ergonomics of JavaScript interop.
4. The ergonomics of TypeScript interop.
5. Transparent decisions and diagnostics.

Fickl is developed for large projects that move faster than Fable's release cycle. Its changes are offered back to Fable, and the differences are tracked so that a claim leaves these pages once Fable catches up: see [Differences from Fable](../performance/differences.md).

## What it adds

- **A faster compiler.** A per-user package cache, a background compile server and a faster watch loop. [Performance](../performance/index.md) shows the benchmarks each release runs against the latest Fable.
- **Native JavaScript.** Computation expressions for `async` functions, generators and async generators, native disposal with `using`, and bindings for the errors, events and objects JavaScript code meets every day. See [Async and promises](../features/async.md).
- **TypeScript declarations.** `--declaration` writes a `.d.ts` file beside each JavaScript file. See [TypeScript](typescript.md).
- **Typed interop.** Fickl.Core describes TypeScript types F# has no word for: `keyof`, `Record`, `Partial`, type guards, branded strings. See [Types](../features/types.md).
- **Switches for implicit behaviour.** Where Fable changes your code to fit JavaScript, Fickl lets you say no: keep a function curried, keep a binding, keep `Some null` apart from `None`. See [Explicit control](../features/emit.md) and [Compiler flags](../flags/index.md).

## Tests

Fickl runs all of Fable's tests, on .NET and on each target. A test that passes on .NET and fails in JavaScript is a compiler bug; the test is never changed to fit. Features that change existing behaviour stay behind a flag or an attribute in the `Fickl` namespace, so a Fable project compiles the same way until it opts in.

## Where to go next

- [Install Fickl](installation.md) and compile a project.
- Read how Fickl and Fable [stay compatible](compatibility.md).
- Browse the [features](../features/index.md).

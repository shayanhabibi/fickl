---
title: Fickl
description: A fork of the Fable F# compiler focused on the JavaScript and TypeScript experience.
layout: splash
---

<section class="rv-hero">
<div class="rv-hero__copy">
<span class="rv-hero__status"><span class="rv-badge">0.9</span> Compatible with Fable projects.</span>
<h1 class="rv-hero__title">fickl</h1>
<p class="rv-hero__line">F# to JavaScript and TypeScript, faster and more explicit.</p>
<p class="rv-hero__sub">A fork of the Fable compiler that adds what JavaScript and TypeScript projects need: native async and generators, TypeScript declarations, typed interop and a switch for every implicit change.<br/>Swap <code>dotnet fable</code> for <code>dotnet fickl</code>; Fable's attributes and functions keep working.</p>
<div class="rv-hero__actions">
<a class="rv-btn rv-btn--primary" href="/fickl/guide/installation/">Get started <svg aria-hidden="true" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14"/><path d="m12 5 7 7-7 7"/></svg></a>
<a class="rv-btn rv-btn--secondary" href="/fickl/features/">Features</a>
</div>
</div>
<div class="rv-demo fk-demo">

```fsharp fickl
open Fable.Core
open Fickl.Core

type User = { Name: string; Age: int }

[<Global>]
let fetchUser (id: int) : JS.Promise<User> = jsNative

[<AsyncFunction>]
let adultNames () = jsAsync {
    let! first = fetchUser 1
    and! second = fetchUser 2
    return
        [ first; second ]
        |> List.filter (fun u -> u.Age >= 18)
        |> List.map _.Name
}
```

<p class="fk-demo__caption">Compiled by this build of Fickl when the site was built.</p>
</div>
</section>

## Why Fickl

Fickl is a lab for the JavaScript and TypeScript side of Fable. Its priorities, in order: the performance and ergonomics of the compiler, explicit control of the code it emits, the performance and ergonomics of JavaScript interop, TypeScript interop, and transparent diagnostics.

::::cards
:::card title="Faster compiles" href="/fickl/performance/"
A package cache, a compile server and a faster watch loop. Every release is benchmarked against the latest Fable.
:::
:::card title="Native JavaScript" href="/fickl/features/async/"
`jsAsync` and `promise` compile to `async` functions, `jsGenerator` to `function*`, and `use` works with `Symbol.dispose`.
:::
:::card title="TypeScript declarations" href="/fickl/guide/typescript/"
`--declaration` writes a `.d.ts` next to each JavaScript file, checked against a strict `tsconfig`.
:::
:::card title="Typed interop" href="/fickl/features/types/"
`keyof`, `Record`, `Partial`, branded strings, type guards, callable interfaces and global augmentations.
:::
:::card title="Explicit by default" href="/fickl/features/emit/"
Every implicit change has a switch: keep a function curried, keep a binding, keep `Some null` apart from `None`.
:::
:::card title="Still Fable" href="/fickl/guide/compatibility/"
Fable's attributes and functions keep working. Fickl's own live in `Fickl.Core`, behind flags or the `FICKL_COMPILER` define.
:::
::::

## What Fickl gets right that Fable does not

Each case in the repository's `tests/Differences` is compiled with both compilers and run with Node.js on every change to `main`. A case leaves the list once Fable passes it too. See [Differences from Fable](performance/differences.md) for the current list.

---
title: Compatibility with Fable
description: How Fickl keeps Fable projects compiling, and how to opt in to Fickl's features.
---

Fickl compiles Fable projects. You can switch to it for the compiler speed alone, and try its features one at a time.

## Fable code keeps working

Fable's attributes and functions stay in the `Fable` namespaces and keep their meaning. Fickl puts what it adds in its own namespaces, `Fickl.Core` and `Fickl.Core.JS`, so nothing changes until you open them.

When Fickl gives an attribute that Fable also has extra options, the options are on the attribute of the same name in `Fickl.Core`. `[<Fable.Core.AttachMembers>]` behaves as in Fable; `[<Fickl.Core.AttachMembers(false)>]` adds the switch to turn it off.

Changes that would alter how existing code behaves at run time stay behind a compiler flag. `--strictOptions` is the example: it keeps `Some null` apart from `None`, as .NET does, which is more correct and also changes what existing code sees. See [Compiler flags](../flags/index.md).

## FICKL_COMPILER

Fickl defines `FICKL_COMPILER`, alongside `FABLE_COMPILER`. Use it to choose between Fickl.Core and Fable.Core where both have something:

```fsharp fickl
let compiler =
#if FICKL_COMPILER
    "fickl"
#else
    "fable"
#endif
```

Only the Fickl branch is compiled. Fable skips it and compiles the other.

## Behaviour that differs

Some of Fickl's changes are fixes rather than features: they make output match .NET where Fable's does not. Each one is a test case in the repository's `tests/Differences`, compiled with both compilers and run with Node.js. A case is removed when Fable passes it too. [Differences from Fable](../performance/differences.md) lists the cases that still differ.

A few changes alter the runtime representation of a type. They are listed in the release notes, with what they break:

- In 0.9, `decimal` is represented with a native `BigInt` instead of big.js. JavaScript that calls big.js methods on an F# decimal, such as `d.toFixed(2)`, no longer works.

---
title: --strictOptions
description: Keep Some null apart from None, as .NET does.
---

By default, Fickl and Fable erase an option: `Some x` is `x` and `None` is `undefined`. That is fast and reads naturally from JavaScript, but for a type that can itself be null, `Some null` and `None` become the same value. `--strictOptions` keeps them apart.

## Without the flag

`Some null` is `None`, so the second case is never reached:

```fsharp fickl show=js
let describe (name: string option) =
    match name with
    | None -> "none"
    | Some null -> "some null"
    | Some n -> n

let result = describe (Some null)
```

## With the flag

An option of a type that can be null is built with `some` and read with `value`, so `Some null` stays `Some`, as on .NET. It is built the same way at concrete and generic call sites.

```fsharp fickl flags="--strictOptions" show=js
let describe (name: string option) =
    match name with
    | None -> "none"
    | Some null -> "some null"
    | Some n -> n

let result = describe (Some null)
```

Options of types that cannot be null, such as `int option`, are erased either way.

## For interop

A value option, `voption`, stays erased with or without the flag. Use it for values that cross into JavaScript, where `undefined` is the expected way to say "nothing".

:::warning
The flag changes what existing code sees at run time: JavaScript that receives an F# `string option` gets a wrapped value for `Some null`. Turn it on for a whole program, and test the code that talks to JavaScript.
:::

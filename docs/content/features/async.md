---
title: Async functions
description: Write native JavaScript async functions with jsAsync and promise, and bridge F# cancellation to AbortSignal.
---

This page covers the `jsAsync { }` and `promise { }` computation expressions, which compile to native `async` functions, and the conversions between an F# `CancellationToken` and the web `AbortSignal`. You need them when your F# code awaits JavaScript promises, or when it passes cancellation to `fetch` and other cancellable JavaScript APIs.

## `jsAsync` and `promise`

`jsAsync { }` compiles to a native `async` function that returns a `JS.Promise`. Each `let!` is an `await`, and `return` resolves the promise. `promise { }` is the same builder under the name Fable.Promise uses, so existing `promise { }` code compiles to native async functions too. Both live in `Fickl.Core`.

```fsharp fickl
open Fable.Core
open Fickl.Core

let addLater (first: JS.Promise<int>) (second: JS.Promise<int>) = jsAsync {
    let! a = first
    let! b = second
    return a + b
}
```

The body is an async arrow function that is called at once, so the work starts running when you call `addLater`, not when something awaits the result. There is no F# cancellation token in a `jsAsync` body: pass one to the work yourself, as described in [Cancellation](#cancellation).

`try ... with` catches a rejected promise as well as a thrown exception, `try ... finally` runs after the promise settles, and `use` disposes its resource after the body settles.

## Awaiting several promises with `and!`

`let! ... and! ...` awaits its promises together with `Promise.all` instead of one after the other. You can join up to six promises this way.

```fsharp fickl
open Fable.Core
open Fickl.Core

let describe (name: JS.Promise<string>) (count: JS.Promise<int>) = promise {
    let! n = name
    and! c = count
    return $"{n}: {c}"
}
```

The promises are passed to `Promise.all` as they are, so they run at the same time only if they were started before the `let!`. A promise that rejects rejects the whole computation.

## `for` over a promise and over an async iterable

`for value in source do` has two forms besides the ordinary loop over a sequence. Over a `JS.AsyncIterable<'T>` it compiles to `for await`, and leaving the loop early closes the iterator. Over a single `JS.Promise<'T>` it awaits the promise and runs the body once with its value.

```fsharp fickl
open Fable.Core
open Fickl.Core

let total (values: JS.AsyncIterable<int>) = jsAsync {
    let mutable sum = 0
    for value in values do
        sum <- sum + value
    return sum
}

let double (answer: JS.Promise<int>) = jsAsync {
    for value in answer do
        return value * 2
}
```

A `JsAsyncGenerator` is a `JS.AsyncIterable`, so you can loop over the async generators from [Generators](generators.md) the same way.

## `[<AsyncFunction>]`

Without the attribute, a function returning `jsAsync` is an ordinary function that calls an async arrow function. Put `[<AsyncFunction>]` on a module function returning `jsAsync` and the function itself is the `async function`.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<AsyncFunction>]
let fetchTwice (promise: JS.Promise<int>) = jsAsync {
    let! first = promise
    let! second = promise
    return first + second
}
```

The compiled value is a `const` bound to an `async function` expression, so it has its own `this`, which TypeScript types as `this: any`. The attribute applies to module functions only.

## Cancellation

`Fickl.Core` adds two extension members that convert between a `CancellationToken` and an `AbortSignal`. `token.ToAbortSignal()` is a signal that aborts when the token is cancelled, ready to pass to `fetch`. `CancellationToken.FromAbortSignal(signal)` is a token that is cancelled when the signal aborts, for example the signal of an incoming request.

```fsharp fickl
open System.Threading
open Fable.Core
open Fickl.Core

[<Global>]
let fetch (url: string) (init: obj) : JS.Promise<obj> = jsNative

let load (token: CancellationToken) =
    fetch "/data" {| signal = token.ToAbortSignal() |}

let handle (request: {| signal: JS.AbortSignal |}) =
    let token = CancellationToken.FromAbortSignal(request.signal)
    token.IsCancellationRequested
```

The same token always gives the same signal, and the same signal the same token. A signal from `ToAbortSignal` gives its token back, and the token from `FromAbortSignal` gives its signal back. `JS.AbortSignal` and `JS.AbortController` bind the web types.

### F# `async` and promises

Two overloads connect an F# `async` to promises under cancellation. `Async.AwaitPromise(promise, token)` stops waiting when the token is cancelled, and the computation is then cancelled without waiting for the promise. `Async.StartAsPromise(work, signal)` starts the computation as a promise and cancels it when the signal aborts.

```fsharp fickl
open System
open System.Threading
open Fable.Core
open Fickl.Core

let waitFor (token: CancellationToken) (work: JS.Promise<int>) = async {
    try
        let! value = Async.AwaitPromise(work, token)
        return Some value
    with :? OperationCanceledException ->
        return None
}

let start (signal: JS.AbortSignal) =
    Async.StartAsPromise(Async.Sleep 1000, signal)
```

`AwaitPromise` with a token stops only the wait. To stop the work too, pass the token's `ToAbortSignal()` to the code that made the promise.

### `AbortError`

An aborted `fetch` rejects with a `DOMException` named `"AbortError"`, and cancelling a token aborts its signal with the same error. Fickl treats the two sides as one:

- An `AbortError` is an `OperationCanceledException` for `:?`, so `:? OperationCanceledException` catches an aborted `fetch`.
- An F# `async` awaiting a promise that rejects with an `AbortError` is cancelled, not failed.
- An `OperationCanceledException` is named `"AbortError"`, so JavaScript code that checks `e.name === "AbortError"` sees F# cancellation as an abort. A computation started with `Async.StartAsPromise(work, signal)` rejects with it when the signal aborts.

## `JS.awaitTopLevel`

`JS.awaitTopLevel` awaits a promise at the top level of a module, so a module-level value has the resolved type instead of a `Promise`.

```fsharp fickl
open Fable.Core
open Fickl.Core

type Config = { url: string }

[<Global>]
let loadConfig () : JS.Promise<Config> = jsNative

let config: Config = JS.awaitTopLevel (loadConfig ())
```

This compiles to `export const config = await loadConfig();`. It is allowed only as the whole initializer of a module-level value; anywhere else it is a compile error. On .NET it throws.

:::warning
A top-level `await` changes the order modules run in. A module that imports this one, directly or through others, does not start until the promise settles, and fails to load if it rejects. A slow promise delays the whole program, and a watch-mode reload waits for it too. In an import cycle, a module of the cycle can run before the value is set and read it uninitialized.
:::

TypeScript needs `module` set to `es2022`, `esnext`, `preserve` or `nodenext`, and a target of `es2017` or later.

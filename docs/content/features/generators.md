---
title: Generators
description: Write native JavaScript generators and async generators in F#, and dispose resources the way JavaScript does.
---

This page covers `jsGenerator { }` and `jsAsyncGenerator { }`, which compile to native `function*` and `async function*` generators, and how F# `use` works with JavaScript's `Symbol.dispose` and `Symbol.asyncDispose`. You need them when JavaScript code expects a generator, sends values back through `next`, or iterates your values with `for await`.

## `jsGenerator`

`jsGenerator { }` compiles to a native generator. Its type is `JsGenerator<'Yield, 'Return, 'Next>`: the values it yields, the value it returns when it finishes, and the values the caller sends back with `next`. `yield` yields a value, `yield!` yields every value of a sequence, and `return` finishes the generator with its terminal value.

```fsharp fickl
open Fickl.Core

let countdown (start: int) : JsGenerator<int, string, unit> = jsGenerator {
    let mutable n = start
    while n > 0 do
        yield n
        n <- n - 1
    return "liftoff"
}

let total () =
    let mutable sum = 0
    for n in countdown 3 do
        sum <- sum + n
    sum
```

The body does not run until the first `next` call, as with any JavaScript generator. A `JsGenerator` is also an F# sequence, so `for` and the `Seq` functions iterate it.

### Receiving values from `next`

In a `jsGenerator`, `let! reply = value` yields `value` and binds `reply` to the value the caller passes to the following `next`. The result of `next` is a `JsGeneratorResult`, which you match as `Yielded` or `Returned`.

```fsharp fickl
open Fickl.Core

let accumulate () : JsGenerator<int, unit, int> = jsGenerator {
    let mutable sum = 0
    while true do
        let! added = sum
        sum <- sum + added
}

let run () =
    let gen = accumulate ()
    gen.next () |> ignore
    gen.next 5 |> ignore
    match gen.next 10 with
    | Yielded result -> result.value
    | Returned _ -> 0
```

`let! added = sum` compiles to `const _arg = (yield sum) | 0;`. The generator also has `return`, which closes it and runs its active `finally` blocks, and `throw`, which raises an exception at the suspended `yield`; `try ... with` inside the body catches it.

## `[<GeneratorFunction>]`

Without the attribute, a function returning `jsGenerator` is an ordinary function that creates and returns a generator. Put `[<GeneratorFunction>]` on a module function returning `jsGenerator` and the function itself is the `function*`.

```fsharp fickl
open Fickl.Core

[<GeneratorFunction>]
let conversation (request: string) : JsGenerator<string, string, string> = jsGenerator {
    let! reply = request
    yield reply
    return "done:" + reply
}
```

The compiled value is a `const` bound to a `function*` expression, typed `Generator<string, string, string>` in TypeScript. The return type annotation gives the yielded, terminal and sent types.

## `jsAsyncGenerator` and `jsYield`

`jsAsyncGenerator { }` compiles to a native async generator, a `JsAsyncGenerator<'Yield, 'Return, 'Next>`. In it, `let!` awaits a promise, and `yield!` yields every value of a sequence or of a `JS.AsyncIterable`.

```fsharp fickl
open Fable.Core
open Fickl.Core

let pages (fetchPage: int -> JS.Promise<string[]>) : JsAsyncGenerator<string, unit, unit> = jsAsyncGenerator {
    let mutable page = 0
    let mutable more = true
    while more do
        let! items = fetchPage page
        yield! items
        more <- items.Length > 0
        page <- page + 1
}
```

Because `let!` awaits, receiving a value from `next` needs its own form: `let! reply = jsYield value` yields `value` and binds `reply` to the value sent by the following `next`. Yield and return resolved values, not promises.

## `[<AsyncGeneratorFunction>]`

`[<AsyncGeneratorFunction>]` on a module function returning `jsAsyncGenerator` emits it as an `async function*`. A `JsAsyncGenerator` is a `JS.AsyncIterable`, so a `jsAsync` body loops over it with `for await`.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<AsyncGeneratorFunction>]
let conversation (request: JS.Promise<string>) : JsAsyncGenerator<string, string, string> = jsAsyncGenerator {
    let! ready = request
    let! reply = jsYield ready
    yield reply
    return "done:" + reply
}

[<AsyncFunction>]
let consume (source: JS.AsyncIterable<string>) = jsAsync {
    let mutable result = ""
    for value in source do
        result <- result + value
    return result
}
```

See [Async functions](async.md) for `jsAsync` and `[<AsyncFunction>]`.

## Native disposal

JavaScript's explicit resource management uses `Symbol.dispose` and `Symbol.asyncDispose`, with the `using` and `await using` declarations. Fickl connects them to F# disposal in both directions:

- F# `use` disposes an object that implements `Symbol.dispose`, as well as one that implements `IDisposable`. A `use` of an `IAsyncDisposable` in a `task`, and a `use` in a `jsAsyncGenerator`, awaits `Symbol.asyncDispose` when the object has no `DisposeAsync`.
- An F# class or object expression implementing `IDisposable` gets a `[Symbol.dispose]` method that calls `Dispose`, so JavaScript can hold it with `using`. One implementing `IAsyncDisposable` works with `await using`.

```fsharp fickl
open System
open Fable.Core

[<Global>]
let openFile (path: string) : IDisposable = jsNative

let describe (path: string) =
    use file = openFile path
    string file

type Lock() =
    interface IDisposable with
        member _.Dispose() = printfn "released"
```

The `use` compiles to a `try ... finally` that calls `disposeSafe`, which calls `Dispose` if the object has one and `[Symbol.dispose]` otherwise. When an object has both, `Dispose` runs. `Lock` compiles with a `[disposeSymbol]()` method, the runtime library's name for `Symbol.dispose`.

In a `jsAsyncGenerator`, `use` takes a resource that implements `IDisposable`, `IAsyncDisposable` or `JsAsyncDisposable`, the `Fickl.Core` binding of an object with `Symbol.asyncDispose`, and awaits its disposal when the generator leaves its body. The compiled `finally` calls `disposeAsyncSafe`, which tries `DisposeAsync`, then `[Symbol.asyncDispose]`, then synchronous disposal.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<Global>]
let connect () : JsAsyncDisposable = jsNative

let rows () : JsAsyncGenerator<int, unit, unit> = jsAsyncGenerator {
    use db = connect ()
    yield 1
    yield 2
}
```

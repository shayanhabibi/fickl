---
title: Errors
description: Catch the errors JavaScript throws by type, and choose when F# exceptions are native JavaScript errors.
---

This page covers how F# exceptions and JavaScript errors meet: catching a JavaScript `TypeError` or a thrown string in F#, making F# exceptions real `Error` objects for stack traces and error reporters, and raising `DivideByZeroException` as .NET does. You need it whenever F# code calls JavaScript that can throw, or JavaScript code catches what F# throws.

## Catching JavaScript errors by type

`Fickl.Core.JS` binds the errors JavaScript throws: `JS.Error`, `JS.TypeError`, `JS.RangeError`, `JS.SyntaxError`, `JS.ReferenceError`, `JS.AggregateError` and `JS.DOMException`. Each is the JavaScript global of the same name, so `:? JS.SyntaxError` compiles to `instanceof SyntaxError`. They have `message`, `name`, `stack` and `cause`.

```fsharp fickl
open Fable.Core
open Fickl.Core

let tryParse (text: string) =
    try
        Some (JS.JSON.parse text)
    with :? JS.SyntaxError as e ->
        printfn "%s: %s" e.name e.message
        None
```

You can also construct and raise them. `raise (JS.RangeError "...")` compiles to `throw new RangeError(...)`, which JavaScript callers catch as a plain `RangeError`.

```fsharp fickl
open Fickl.Core

let checkIndex (index: int) (length: int) =
    if index < 0 || index >= length then
        raise (JS.RangeError $"index {index} is outside 0..{length - 1}")
```

`JS.AggregateError` has the `errors` it holds, and `JS.DOMException` is told apart by its `name`, such as `"AbortError"` or `"NotFoundError"`. The bindings inherit `exn` only so that a handler can test for them: an F# exception is not a `JS.Error` unless it is compiled as one, as described below.

:::note
With `<Nullable>enable</Nullable>`, a `:? JS.TypeError` pattern on an `exn`, and `raise` of one of these bindings, gives a nullness warning (FS3261), because the bindings allow `null`. The code compiles and runs as shown.
:::

## Thrown values that are not errors

JavaScript can throw anything: a string, a plain object, `undefined`. The `JS.Thrown` active pattern matches a caught value that is neither a JavaScript `Error` nor an F# exception, and passes it as it was thrown. Put it after the type tests.

```fsharp fickl
open Fable.Core
open Fickl.Core

[<Global>]
let runPlugin () : unit = jsNative

let classify () =
    try
        runPlugin ()
        "ok"
    with
    | :? JS.TypeError as e -> "TypeError: " + e.message
    | :? JS.DOMException as e when e.name = "AbortError" -> "aborted"
    | :? JS.Error as e -> e.name
    | JS.Thrown value -> "thrown " + string value
    | e -> e.Message
```

On .NET every thrown value is an exception, so `JS.Thrown` never matches there. An F# `async` turns a thrown value that is not an error into an exception, so it does not match inside one either.

### `e.Message` of a thrown value

When you catch a thrown value as `exn` and read `e.Message`, Fickl gives `String(e)` for a value that is not an object. After `throw "boom"`, `e.Message` is `"boom"`, and after `throw undefined` it is `"undefined"`, instead of `undefined` or a `TypeError`. In the example above, the last case compiles to `exceptionMessage(e_4)`.

## `[<JsError>]`

By default an F# exception compiles to a plain object, not an `Error`, so it has no `stack` and error reporters do not print it as an error. Mark an exception `[<JsError>]` and it extends a native `Error` subclass, with a `stack` captured when it is created.

```fsharp fickl
open Fickl.Core

[<JsError>]
exception Timeout of milliseconds: int

let wait () =
    try
        raise (Timeout 500)
    with Timeout ms ->
        ms
```

`Timeout` compiles to `class Timeout extends FSharpError`. It still matches, compares and prints as an F# exception, and its inner exception is also its `cause`. Only this exception pays for the stack capture.

## `--nativeErrors`

`--nativeErrors` does what `[<JsError>]` does for every exception, from `failwith`, `invalidArg` and the runtime library to a declared F# exception. Each is `instanceof Error`, has a `stack`, and has its inner exception as its `cause`, so Node.js, test runners and error reporters print it as an error.

```fsharp fickl flags="--nativeErrors"
exception Invalid of field: string

let parse (text: string) =
    if text = "" then raise (Invalid "text")
    try
        int text
    with e ->
        raise (System.FormatException("not a number", e))
```

The compiled module calls `enableNativeErrors()` from the runtime library when it loads. The flag affects the whole program, including libraries compiled without it.

:::warning
A native `Error` captures a stack trace when it is created. Without the flag, an F# exception is a plain object and is about 50 times faster to throw ([Fable #2160](https://github.com/fable-compiler/Fable/issues/2160)). Code that throws exceptions for control flow pays that cost on every throw. If only a few exceptions need a stack, mark those with `[<JsError>]` instead.
:::

## `--checkedDivision`

On .NET, dividing an integer or a decimal by zero raises `DivideByZeroException`. In JavaScript without the flag, a 32-bit or smaller integer division by zero gives `0`, and `int64`, `bigint` and `decimal` throw an error of the runtime library that `:? DivideByZeroException` does not catch. `--checkedDivision` checks the divisor of `/`, `%` and `Math.DivRem` and raises `DivideByZeroException` when it is zero.

```fsharp fickl flags="--checkedDivision"
let average (total: int) (count: int) =
    try
        total / count
    with :? System.DivideByZeroException ->
        0
```

With the flag, `total / count` compiles to `~~(total / nonZero(count)) | 0`, where `nonZero` raises the exception. A divisor that is a constant other than zero is not checked.

See [Compiler flags](../flags/index.md) for the other flags.

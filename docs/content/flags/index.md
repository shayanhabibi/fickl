---
title: Compiler flags
description: The command-line flags Fickl adds to Fable's, and what each one changes.
---

Fickl takes all of Fable's arguments. This page lists the flags it adds. Run `dotnet fickl --help` for the full list.

Flags that change generated code apply to the whole compilation, including the packages compiled with it.

## Output

### --declaration

Writes a TypeScript declaration file (`.d.ts`) next to each JavaScript file, describing what that file exports. JavaScript output only. See [TypeScript](../guide/typescript.md).

### --fileExtension

Sets the extension of the file generated from one source file, as `<file>=<extension>`: `--fileExtension vite.config.fs=.js` writes `vite.config.js`. Can be repeated.

### --redirect

Imports a project or Fable package reference from an npm package instead of compiling it into the output: `--redirect MyLib.Core=@me/mylib-core`. Can be repeated. The package must hold the reference's own Fickl output, laid out as its sources are.

## Semantics

### --strictOptions

Keeps `Some null` apart from `None` for options of types that can be null: strings, arrays, classes, records, functions and `Nullable`. Without it, such an option is erased, which is faster and reads as `T | undefined` in TypeScript, but `Some null` is `None`. Value options stay erased either way, so `voption` is the type to use for interop. See [--strictOptions](strict-options.md).

### --checkedDivision

Raises `DivideByZeroException` when an integer or a decimal is divided by zero, as on .NET. Without it, a 32-bit or smaller integer division by zero gives `0`, and `int64`, `bigint` and `decimal` throw a JavaScript error that `:? DivideByZeroException` does not catch. A divisor that is a constant other than zero is not checked.

```fsharp fickl flags="--checkedDivision" show=js
let ratio (a: int) (b: int) = a / b
```

### --nativeErrors

Makes every exception, from `failwith` and `invalidArg` to a declared F# exception, a JavaScript `Error` with a `stack` and its inner exception as its `cause`, so Node.js, test runners and error reporters print it as an error. Without it, an exception is a plain object and about 50 times faster to throw, because a native `Error` captures a stack trace when it is created. To pay that cost for one exception type only, mark it `[<JsError>]`. See [Errors](../features/errors.md).

### --clampByteArrays

Compiles byte arrays as `Uint8ClampedArray`, the type canvas pixel data uses, so they can be passed to it without a copy. Out-of-range values clamp to 0 or 255 instead of wrapping as on .NET.

### --privateFields

Compiles the `let` fields of every class with attached members to ES `#private` fields, which JavaScript cannot read or enumerate. Classes that inherit a JavaScript class do this without the flag. See [Classes](../features/classes.md).

### --temporal

Represents `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` and `TimeSpan` with the JavaScript Temporal API. Experimental, and needs a runtime with Temporal.

## Debugging

### --debugPipelines

Compiles each stage of a `|>`, `||>` or `|||>` pipeline to a statement that binds its result, each mapped to the source line of its stage, so a debugger can step through the stages and inspect their results.

```fsharp fickl flags="--debugPipelines" show=js
let total (xs: int list) =
    xs
    |> List.filter (fun x -> x > 0)
    |> List.map (fun x -> x * 2)
    |> List.sum
```

### --timings

Prints a per-phase timing and allocation report after each compilation.

## Compiler speed

### --server

Compiles through a background server that keeps check results between compilations, so recompiling a project only rechecks what changed. `FICKL_SERVER=1` turns it on too, and `FICKL_SERVER=0` turns it off. See [Performance](../performance/index.md).

### --noPackageCache

Type-checks package sources instead of reusing them from Fickl's package cache. The cache is per user and keyed by the packages' contents and the compiler options, so it is safe to leave on.

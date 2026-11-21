---
title: Performance
description: How Fickl compiles faster than Fable, the benchmarks each release runs against it, and array access without bounds checks.
---

Compiler speed is Fickl's first priority. Each release is benchmarked against the latest Fable, and the charts below are the ones the release workflow drew for the latest release. The gaps should close over time, as Fickl's changes are offered back to Fable.

## Benchmarks

{{readme:benchmarks}}

## Where the time goes

### Package cache

Type-checked package sources are cached per user, keyed by their contents and the compiler options, so a later compile, in any project, skips checking them again. `--noPackageCache` turns it off. It is off by default in CI builds; `--packageCache` turns it on there (see [Compiler flags](../flags/index.md)).

### Compile server

`--server`, or `FICKL_SERVER=1`, compiles through a background server that keeps check results between compilations. Running the compiler again on the same project only rechecks what changed.

```bash
dotnet fickl src/App.fsproj -o dist --server
```

### Watch mode

`dotnet fickl watch` recompiles what a change affects. The second chart above measures the time from saving a file to the compiler reporting `Watching`.

### Timings

`--timings` prints a per-phase timing and allocation report after each compilation, to see where a slow project spends its time.

## Array bounds checks

Array reads and writes, and string reads, are checked as on .NET, so an index out of range throws. For numeric code that is known to be correct, `[<BoundsChecks(false)>]` from `Fickl.Core` compiles the reads and writes inside a module, type, member or `let` to plain `a[i]` and `a[i] = v`, without a comparison or a library call.

```fsharp fickl
[<Fickl.Core.BoundsChecks(false)>]
module Kernels

let smooth (a: float[]) (out: float[]) =
    for i in 1 .. a.Length - 2 do
        out.[i] <- (a.[i - 1] + a.[i] + a.[i + 1]) / 3.
```

To turn the checks off in every file of a project, set `FicklBoundsChecks` in its project file:

```xml
<PropertyGroup>
  <FicklBoundsChecks>false</FicklBoundsChecks>
</PropertyGroup>
```

- Both apply to Release builds only. `fickl watch` and `-c Debug` keep every check, so an index out of range still throws while you develop.
- The nearest attribute wins, and the project setting is the outermost scope: `[<BoundsChecks(true)>]` on a member turns the checks back on inside an unchecked module or project. An `inline` function keeps the setting of the scope it is written in, wherever it is inlined.
- The project setting covers the project's own source files. A referenced project and the sources of a package keep their checks unless they set it or use the attribute themselves.
- It covers arrays, typed arrays, 2D arrays, `ResizeArray` and `IList<'T>`, and string reads (`s.[i]` and `s.Chars(i)`), in JavaScript and TypeScript. In TypeScript, each read carries a non-null assertion, `(a[i])!`, so the output type-checks under `noUncheckedIndexedAccess`.
- Out of range, there is no `IndexOutOfRangeException`: a read gives `undefined`, which float arithmetic turns into `NaN` and int arithmetic into `0`, and a string read gives `undefined` instead of a character; a write past the end of a plain array grows it; a typed array ignores the write. Only a 2D row index out of range throws, a `TypeError`, as its row is `undefined`.
- .NET and the other targets always check.

---
title: Performance
description: How Fickl compiles faster than Fable, and the benchmarks each release runs against it.
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

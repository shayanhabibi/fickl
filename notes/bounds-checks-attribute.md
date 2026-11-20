# `[<BoundsChecks(false)>]`: array access without bounds checks in marked code (design, 2026-11-20)

Issue #473, from goswinr's comment on #466.

## Why

Numeric code that is known to be correct pays for bounds checks on every array read and write. Since #466, a read or
write of a local array with a simple index is checked inline, but any other access still calls the library's shared
`item` or `setItem`. V8 keeps type feedback per function (PR #466), so those shared calls run generic once an app uses
several kinds of array. Numeric code mostly indexes with expressions (`i + 1`, `3 * k + d`), which take that path.

What `a1c8e2dcf` emits in Release (measured here, a probe compiled with `-c Release`):

| F# | JavaScript |
| --- | --- |
| `a.[i]`, local array | `a[i] ?? item(i, a)` |
| `a.[i + 1]`, `b.Pos.[i - 1]` | `item(i + 1, a)` |
| `a.[i] <- v`, local array | `((i >= 0) && (i < a.length)) ? (a[i] = v) : setItem(a, i, v)` |
| `a.[i + 1] <- v`, `Array.set`, `ResizeArray` setter | `setItem(a, i + 1, v)` |
| `b.Pos.[i] <- v`, `g.[i, j] <- v` | `b.Pos[i] = v` (no check today) |

## What it does

An attribute in Fickl.Core, `BoundsChecksAttribute(enabled: bool)`. In a Release build, the array reads and writes
written inside code marked `[<BoundsChecks(false)>]` compile to plain `a[i]` and `a[i] = v`.

- **Release only** (user's decision). Release is `com.Options.DebugMode = false`: a build without `-c` (the CLI default),
  or any configuration other than `Debug`. `fickl watch`, which defaults to Debug, and `-c Debug` keep every check, so
  an out-of-range bug still throws while developing.
- **Where it goes:** a module, a class or other type, a member, or a module-level `let` function or value. F# allows no
  attributes on a `let` inside an expression.
- **The nearest attribute wins:** the member or value being compiled, then the type that declares it, then each
  enclosing module, outward. `[<BoundsChecks(true)>]` turns checks back on inside an unchecked scope. Without any
  attribute, code is checked as today.
- **Lexical:** the attribute applies to the code written inside the scope. An `inline` function follows its own scope
  wherever it is inlined, in both directions. An inline body defers its replacements to each call site
  (`UnresolvedReplaceCall`), so `FSharp2Fable.Util.inlineExpr` sets the scope from the inline member's attributes and
  declaring entities before it resolves them.
- **TypeScript:** each unchecked read is `(a[i])!`, so the output type-checks under `noUncheckedIndexedAccess`. In an
  emit, `$0!` is a raw string argument (#197), hence the parentheses.
- **Accesses covered:** `'T[]` and typed arrays (`a.[i]`, `a.[i] <- v`, `Array.get`, `Array.set`, `Array.item`),
  2D arrays (`a.[i, j]`, `a.[i, j] <- v`, `Array2D.get`, `Array2D.set`), and the `ResizeArray` indexer.
- **No effect on .NET**, where the runtime always checks.

Without checks, an out-of-range read gives `undefined` (NaN in arithmetic), a write past the end of a plain array grows
it, and a typed array drops the write. The documentation says so.

## Goals, non-goals, anti-goals

- Goal: in Release, marked code indexes arrays with no comparison and no library call.
- Non-goal: the temporary-variable index for code without the attribute (approach B below); a possible later PR.
- Non-goal: a project-wide switch; `[<assembly: BoundsChecks(false)>]` could be added later.
- Non-goal: Python, Rust, Dart, PHP and BEAM.
- Non-goal: library range functions (`Array.sub`, `Array.blit`, `Array.fill`, slices) keep their argument checks.
- Non-goal: the writes through a field and into 2D arrays that are unchecked today stay as they are; a separate fix.
- Anti-goal: any change to the output of code without the attribute. An integration snapshot that changes outside the
  new tests is a failure.
- Anti-goal: dropping a check in a Debug build.

## Approaches

| | Approach | Trade-off |
| --- | --- | --- |
| **A (picked)** | The scoped attribute. The scope is decided where array accesses are rewritten (`Replacements.fs`, with the FSharp2Fable context), and the result is carried in the Fable AST | Contained; code without the attribute is untouched |
| B | A, plus binding a computed index to a temporary so #466's inline form applies to all code | Most of the speed for every app, .NET behaviour kept; changes a lot of existing output and snapshots |
| C | A project-wide flag or MSBuild property instead | Smallest; unchecks code nobody reviewed for it, against "known to be correct" |

Deciding in Fable2Babel instead of the replacements was rejected: by then an inlined body has its caller's scope, and
reads are already rewritten to library calls.

## Calls made in this design (not discussed)

- The name `BoundsChecks` and the `enabled: bool` argument, after `PrivateFields(enabled)`. No argumentless form.
- Attribute targets: class, struct, method, property, constructor. A module is a class, a `let` value a property.
- Top-level module code (a module-level `do` or loop) follows its module's attribute.
- Fable.AST is a published package, so the unchecked write is carried without changing it.
- Documented in a new section of `docs/content/performance/index.md`, and a row in `notes/transformation-toggles.md`,
  since bounds checks are a correctness rewrite and this is its escape hatch.

## As built

- The scope is computed once per member into `Context.BoundsChecked` (`FSharp2Fable.Util.isBoundsCheckedMember`), and
  per module for its top-level code, only for a Release build of JavaScript or TypeScript (`canUncheckArrays`).
- An inline expansion takes the scope from the FCS member when it has one. A call deferred inside another inline body
  reads it through the member reference (`isBoundsCheckedCall`), and a member that does not resolve is checked. A named
  function passed to an `[<InlineIfLambda>]` parameter is compiled again at the call site, under its own scope.
- `static let` and `static do` of a type compile as actions of the enclosing module, so they follow the module's
  attribute rather than the type's.
- An unchecked write is the emit `$0 = $1`, whose target is the indexed access, since Fable2Babel's `transformSet`
  checks a `Set` of a local array.
- Tests: `tests/Integration/Integration/data/boundsChecks` (JavaScript, Release), `data/boundsChecksTs` (TypeScript
  under `noUncheckedIndexedAccess`), `data/boundsChecksLibrary` (inline functions of a precompiled library),
  `boundsChecksDebug` (the same source in Debug, unchanged output), and the `BoundsChecks(false)` cases in
  `tests/Js/Main/ArrayTests.fs`.

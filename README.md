# Fickl: Fable Performance Lab

This repository is the fork of [Fable](https://github.com/fable-compiler/fable) that refrains from work on bringing cross-language targetting to parity, and instead focuses on three main objectives, in order of priority:

1. Performance and ergonomics of the Fable compiler
2. Explicit control of expression
3. Transparency

> **Fable Compatibility**
>
> Fickl is fully compatible with compiling **any** fable code. However, since fickl includes bug fixes that are not yet upstream, there might be differences in the compiled output.
>
> In contrast, you cannot expect fickl only attributes (which are contained in `Fickl.` prefixed modules) to exist in fable.

## Performance and ergonomics

The fable compiler owns a slice of the F# Compiler, and distributes it as a tool. This includes: a slim version of FSharp.Compiler.Services, and FSharp.Core.

This means there is no concern for the compiler itself to keep target frameworks beyond anything other than what is in LTS. At the moment, this means .NET 10+.
This also benefits the ecosystem of plugins, as they also benefit from modern performance constructs.

Fickl has already made significant strides in improving performance. The difficulty ceiling for completing these tasks is remarkably low in the advent of AI. The importance is in ensuring there is a solid foundation of *behavioural* tests that cover edge cases. There is more yield from prompting AI to try its best to find code utilising/stressing a feature and won't compile, or will compile and provide the wrong runtime behaviour.

<!-- fickl-benchmarks:start -->
![Fickl 0.7.1 against Fable 5.24.0: up to 7.5× faster end to end](notes/benchmarks/fickl-0.7.1.svg)

Wall clock of a fresh process with `--noCache`, median of 5 interleaved runs on Ubuntu 24.04.5 LTS, measured by the release workflow. Raw timings are in [notes/benchmarks/fickl-0.7.1.tsv](notes/benchmarks/fickl-0.7.1.tsv).

![Fickl 0.7.1 against Fable 5.24.0 in watch mode: up to 71.9× faster to respond](notes/benchmarks/fickl-0.7.1-watch.svg)

Time from saving a file to `Watching` in one `--watch` session per compiler. Raw timings are in [notes/benchmarks/fickl-0.7.1-watch.tsv](notes/benchmarks/fickl-0.7.1-watch.tsv).
<!-- fickl-benchmarks:end -->

## Differences from Fable

<!-- fickl-differences:start -->
What Fickl gets right that Fable 5.24.0 does not, checked by compiling each case in [tests/Differences](tests/Differences) with both and running it with node. A case leaves this table once Fable passes it too.

| Change | Fickl | Fable 5.24.0 | Issues |
| --- | --- | --- | --- |
| `[<CallableInterface>]` | An object expression implementing a callable interface compiles to a plain JavaScript function, and keeps a generic `Invoke` generic. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#8](https://github.com/shayanhabibi/fickl/issues/8), [#10](https://github.com/shayanhabibi/fickl/issues/10), [#11](https://github.com/shayanhabibi/fickl/issues/11) |
| `--clampByteArrays` | Compiles byte arrays as `Uint8ClampedArray`, the type canvas pixel data uses, so they can be passed to it without a copy. | doesn't compile: Unknown argument: --clampbytearrays | [#39](https://github.com/shayanhabibi/fickl/issues/39) |
| `[<Curried>]` on a parameter | Keeps a function parameter curried, for JavaScript that calls it one argument at a time. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#24](https://github.com/shayanhabibi/fickl/issues/24) |
| `[<Curried>]` on a record field | Keeps a function stored in a record field curried, for a JS object that stores or calls it one argument at a time. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#41](https://github.com/shayanhabibi/fickl/issues/41) |
| `--declaration` | Writes a TypeScript declaration file next to each JavaScript file, so TypeScript can call the compiled F#. | doesn't compile: Unknown argument: --declaration | [#20](https://github.com/shayanhabibi/fickl/issues/20), [#21](https://github.com/shayanhabibi/fickl/issues/21) |
| `[<CaseTest>]` on erased union cases | A JavaScript predicate tells apart erased cases with the same payload type, here two strings. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#7](https://github.com/shayanhabibi/fickl/issues/7) |
| an explicit class inheriting another explicit class | The class extends its base and keeps the base's fields, so `{ inherit Base("derived"); Extra = 5 }` has its `Name`. | compiles, but fails at runtime: expected derived, got undefined | [#87](https://github.com/shayanhabibi/fickl/issues/87) |
| the `FICKL_COMPILER` define | Defines `FICKL_COMPILER`, so code can choose Fickl.Core or Fable.Core when both have an attribute. | compiles, but fails at runtime: expected fickl, got fable | [#43](https://github.com/shayanhabibi/fickl/issues/43) |
| `[<InlineInterface>]` | A `TypeScriptTaggedUnion` case holding one interface compiles as that interface with the tag added, `{ type: "take", fromIndex, toIndex }`. | doesn't type-check: The namespace or module 'Fickl' is not defined. | — |
| `--temporal` | Represents dates and times with the JavaScript Temporal API, a documented flag for Fable's hidden `--test:js-temporal`. | doesn't compile: Unknown argument: --temporal | [#39](https://github.com/shayanhabibi/fickl/issues/39) |
| reading another file's `mutable` value | The read stays before later side effects; it is not inlined past a call that changes the value. | compiles, but fails at runtime: expected 1, got 0 | [#15](https://github.com/shayanhabibi/fickl/issues/15) |
| `[<NoOverloadSuffix>]` | Drops the overload hash from a method's compiled name, `Counter__Add` instead of `Counter__Add_Z524259A4`, so JavaScript can call it by a predictable name. O... | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#35](https://github.com/shayanhabibi/fickl/issues/35) |
| an explicit class inheriting a class with a primary constructor | The class extends its base, so `{ inherit Base("derived"); Extra = 5 }` has its `Name`. | compiles, but fails at runtime: expected derived, got undefined | [#96](https://github.com/shayanhabibi/fickl/issues/96) |
| `%o` in printf formats | Formats in octal, as .NET does; the value is not printed in decimal. | compiles, but fails at runtime: expected 10\|37777777777\|  100, got 8\|-1\|   64 | [#16](https://github.com/shayanhabibi/fickl/issues/16) |
| `[<ReferenceEquality>]` and `[<NoEquality>]` records and unions | Compares them by reference, as .NET does; two values with equal fields are not equal. | compiles, but fails at runtime: expected false, got true | [#30](https://github.com/shayanhabibi/fickl/issues/30) |
| `--strictOptions` | `Some null` of a string, array, class, record or function stays `Some`, as on .NET, built the same way at concrete and generic sites. Value options stay erased. | doesn't compile: Unknown argument: --strictoptions | [#32](https://github.com/shayanhabibi/fickl/issues/32) |
<!-- fickl-differences:end -->

## Explicit control of expression

When targeting another language, a sensible collection of ergonomic changes are made to faithfully represent F# semantics in the target language. Beyond this, are a collection of attributes and other compiler recognised constructs that inform our transformation pipeline.

There should be a distinct combination of target related and target agnostic attributes that provide predictable behaviour in the transformation pipeline. Behaviours should **always** have toggles. And the compiler should **always** recognise these toggles and adjust accordingly. Crossing the bounds of generic types can always make this more difficult to apply correctly.

The capacity to explicitly control transformation via attributes does not have to be as ergonomic as normal usage. We expect explicit control to be used in exception; however, we acknowledge that some libraries/authors/tools will use them in excess, so we will not attempt to deter users; instead, we will attempt to adjust the balance between implicit and explicit.

### Escape hatches

| Toggle | What it changes |
| --- | --- |
| `[<Fickl.Core.Curried>]` on a parameter or record field | Keeps a function parameter, or a function stored in a record field, curried, for JavaScript that calls it one argument at a time. |
| `--strictOptions` | An `option` of a type that can be null (string, array, class, record, function, `Nullable`) keeps `Some null` apart from `None`, as on .NET: it is built with `some` and read with `value`. Without it, such an option is erased, which is faster and reads as `T \| undefined` in TypeScript, but `Some null` is `None`. A `voption` stays erased either way, so it is the type to use for interop. |
| `[<Fickl.Core.NoOverloadSuffix>]` on a member or type | Drops the overload hash from a method's compiled name, `Counter__Add` instead of `Counter__Add_Z524259A4`, so JavaScript can call it by name. On a type it applies to every member that is not overloaded; on an overloaded member it is an error. |
| `--clampByteArrays` | Compiles byte arrays as `Uint8ClampedArray`, the type canvas pixel data uses. Out-of-range values clamp instead of wrapping as on .NET. |
| `--temporal` | Represents `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` and `TimeSpan` with the JavaScript Temporal API (experimental). |

## Transparency

Development loops are increasingly becoming agentic, and workflows are increasingly being inhibited/obstructed by the lack of machine readable diagnostics/logs/information.

An agent developing bindings for you should be able to run a daemon and iterate over types/methods and see their output in real time, and in isolation.
Where possible, we will endeavour towards this, where it does not overly diverge the FCS or underlying Fable compiler implementation.

---

## Fable

As much as possible, the development and learnings that are made in this endeavor should be made available upstream.

## Contribution

The principle behind having this *Lab* is the ability to make changes and publish with speed, and without encumbrance.

Contribution should be made to Fable instead of Fickl. However, if you insist on embarking on this experiment, then you are free to do so. Agentic contributions should endeavour to have full provenance, experimental data, tests, mutations and considered cost/benefit ratios accompanying them.

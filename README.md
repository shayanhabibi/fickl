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
![Fickl 0.7.4 against Fable 5.25.0: up to 7.8× faster end to end](notes/benchmarks/fickl-0.7.4.svg)

Wall clock of a fresh process with `--noCache`, median of 5 interleaved runs on Ubuntu 24.04.5 LTS, measured by the release workflow. Raw timings are in [notes/benchmarks/fickl-0.7.4.tsv](notes/benchmarks/fickl-0.7.4.tsv).

![Fickl 0.7.4 against Fable 5.25.0 in watch mode: up to 88.9× faster to respond](notes/benchmarks/fickl-0.7.4-watch.svg)

Time from saving a file to `Watching` in one `--watch` session per compiler. Raw timings are in [notes/benchmarks/fickl-0.7.4-watch.tsv](notes/benchmarks/fickl-0.7.4-watch.tsv).
<!-- fickl-benchmarks:end -->

## Differences from Fable

<!-- fickl-differences:start -->
What Fickl gets right that Fable 5.26.0 does not, checked by compiling each case in [tests/Differences](tests/Differences) with both and running it with node. A case leaves this table once Fable passes it too.

| Change | Fickl | Fable 5.26.0 | Issues |
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
| `[<JsError>]` | An F# exception marked `[<JsError>]` is a JS `Error` subclass with a `stack`, and still matches and compares as an F# exception. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#157](https://github.com/shayanhabibi/fickl/issues/157) |
| `--temporal` | Represents dates and times with the JavaScript Temporal API, a documented flag for Fable's hidden `--test:js-temporal`. | doesn't compile: Unknown argument: --temporal | [#39](https://github.com/shayanhabibi/fickl/issues/39) |
| reading another file's `mutable` value | The read stays before later side effects; it is not inlined past a call that changes the value. | compiles, but fails at runtime: expected 1, got 0 | [#15](https://github.com/shayanhabibi/fickl/issues/15) |
| `--nativeErrors` | Every exception, from `failwith` to a declared F# exception, is a JS `Error` with a `stack`, and an inner exception is its `cause`. | doesn't compile: Unknown argument: --nativeerrors | [#157](https://github.com/shayanhabibi/fickl/issues/157) |
| `[<NoOverloadSuffix>]` | Drops the overload hash from a method's compiled name, `Counter__Add` instead of `Counter__Add_Z524259A4`, so JavaScript can call it by a predictable name. O... | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#35](https://github.com/shayanhabibi/fickl/issues/35) |
| an explicit class inheriting a class with a primary constructor | The class extends its base, so `{ inherit Base("derived"); Extra = 5 }` has its `Name`. | compiles, but fails at runtime: expected derived, got undefined | [#96](https://github.com/shayanhabibi/fickl/issues/96) |
| `--strictOptions` | `Some null` of a string, array, class, record or function stays `Some`, as on .NET, built the same way at concrete and generic sites. Value options stay erased. | doesn't compile: Unknown argument: --strictoptions | [#32](https://github.com/shayanhabibi/fickl/issues/32) |
<!-- fickl-differences:end -->

## Added functionality

What Fickl adds on top of Fable. The computation expressions, attributes and functions live in the `Fickl.Core` package and target JavaScript and TypeScript.

| Feature | Kind | What it does |
| --- | --- | --- |
| `jsAsync { }` | Computation expression | Compiles to a native `async` function returning a `JS.Promise`: `let!` emits `await`, `for` over a `JS.AsyncIterable` emits `for await`, and it starts running immediately. There is no F# cancellation token. |
| `[<AsyncFunction>]` | Attribute on a module function | Emits a function returning `jsAsync` as a native `async function` declaration. |
| `jsGenerator { }` | Computation expression | Compiles to a native generator, a `JsGenerator<'Yield, 'Return, 'Next>` typed by what it yields, returns and receives from `next`. `let!` binds the value sent back by `next`. |
| `[<GeneratorFunction>]` | Attribute on a module function | Emits a function returning `jsGenerator` as a native `function*` declaration. |
| `jsAsyncGenerator { }`, `jsYield` | Computation expression | Compiles to a native async generator, a `JsAsyncGenerator<'Yield, 'Return, 'Next>`. `let!` awaits a Promise, `let! reply = jsYield value` yields and receives, and `use` awaits `IAsyncDisposable` or `Symbol.asyncDispose`. |
| `[<AsyncGeneratorFunction>]` | Attribute on a module function | Emits a function returning `jsAsyncGenerator` as a native `async function*` declaration. |
| Native disposal | Runtime | F# `use` consumes objects implementing `Symbol.dispose` and `Symbol.asyncDispose`, and compiled `IDisposable` and `IAsyncDisposable` values work with native `using` and `await using`. |
| `jsFunction`, `[<JsFunction>]` | Function, attribute on a parameter | Compiles a lambda as an ordinary `function` with its own `this` and `arguments`, instead of an arrow function. |
| `withThis` | Function | Compiles a lambda as a `function` whose first argument is its `this` receiver, typed as a TypeScript `this` parameter. |
| `[<JsDecorator>]` | Attribute on a class or member | Emits a native decorator, global or imported, on a class, method, property or field. |
| `[<SymbolKey>]` | Attribute on a member or field | Uses a symbol expression, such as `Symbol.iterator` or an imported symbol, as the JavaScript key of a member, interface slot or record field. |
| `[<CallableInterface>]` | Attribute on an interface | An object expression implementing the interface compiles to a plain function, and `Invoke` calls compile to direct calls. A generic `Invoke` stays generic. |
| `[<CaseTest>]` | Attribute on a union case | A JavaScript predicate that identifies an erased union case, or tells apart `TypeScriptTaggedUnion` cases that share a tag. |
| `[<InlineInterface>]` | Attribute on a union or case | Compiles a `TypeScriptTaggedUnion` case holding one interface as that interface with the tag added, `Take & { type: "take" }`. |
| `[<Curried>]` | Attribute on a parameter or field | Keeps a function curried, for JavaScript that passes or calls it one argument at a time. |
| `[<NoOverloadSuffix>]` | Attribute on a member or type | Drops the overload hash from compiled member names, `Counter__Add` instead of `Counter__Add_Z524259A4`. |
| `[<EmitType>]` | Attribute on a type | Writes the type's TypeScript annotation as the given text, with `$0`, `$1`... for its generic arguments, for conditional, mapped and template literal types: ``[<EmitType("`data-${string}`")>]``. A module path imports the name the text starts with as a type. |
| `partial`, `full` | Function | `partial (fun s -> s.count <- 1)` builds an object with some of a type's properties, `{ count: 1 } satisfies Partial<State>` in TypeScript; `full` casts it to the complete type. In TypeScript, `jsOptions` also checks a flat object with `satisfies Partial<T>` before its cast. |
| `Partial<'T>`, `Required<'T>`, `Readonly<'T>` | Type | The TypeScript utility types, for binding signatures such as `abstract setState: Partial<State> -> unit`. |
| Read-only collections in TypeScript | Compiler | `IReadOnlyCollection<T>` and `IReadOnlyList<T>` are annotated `Iterable<T>`, and `IReadOnlyDictionary<K, V>` `IReadOnlyMap<K, V>`, instead of `any`, so TypeScript can pass a `readonly T[]` or `ReadonlyMap`. |
| `Fickl.Core.JS.ReadonlyArray<'T>` | Type | Binds TypeScript's `ReadonlyArray<T>`, for a parameter that accepts `readonly T[]` and `as const` arrays. `ReadonlyArray.ofArray` and `ofResizeArray` view an F# array without a copy. |
| `--declaration` | CLI flag | Writes a TypeScript declaration file (`.d.ts`) next to each JavaScript file. |
| `--strictOptions` | CLI flag | Keeps `Some null` apart from `None`, as on .NET. |
| `--clampByteArrays` | CLI flag | Compiles byte arrays as `Uint8ClampedArray`. |
| `--nativeErrors` | CLI flag | Makes every exception a JS `Error`, with a `stack` and, for an inner exception, a `cause`. |
| `[<JsError>]` | Attribute on an F# exception | Compiles the exception as a JS `Error` subclass, with a `stack`. |
| `JS.Error`, `JS.TypeError`, `JS.RangeError`, `JS.SyntaxError`, `JS.ReferenceError`, `JS.AggregateError`, `JS.DOMException` | Bindings | The errors JavaScript throws, to catch with `:?` (`instanceof`) and construct, with `message`, `name`, `stack` and `cause`. |
| `JS.Thrown` | Active pattern | Matches a caught value that is neither a JS `Error` nor an F# exception, such as a thrown string, and passes it as it was thrown. |
| `e.Message` of a thrown value | Runtime | A caught value typed `exn` that is not an object, such as a thrown string or `undefined`, has `String(e)` as its message instead of `undefined` or a `TypeError`. |
| `--temporal` | CLI flag | Represents dates and times with the JavaScript Temporal API (experimental). |
| `--server` | CLI flag | Compiles through a background server that keeps check results, so a recompile only rechecks what changed. `FICKL_SERVER=1` turns it on too. |
| `--timings` | CLI flag | Prints a per-phase timing and allocation report after each compilation. |
| Package cache | Compiler | Type-checked package sources are cached per user, keyed by their contents and the compiler options, so later compiles skip checking them. `--noPackageCache` turns it off. |
| `FICKL_COMPILER` | Define | Defined when compiling with Fickl, so code can choose between Fickl.Core and Fable.Core. |

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
| `--nativeErrors` | Every exception, from `failwith`, `invalidArg` and the runtime library to a declared F# exception, is `instanceof Error`, has a `stack` and has its inner exception as its `cause`, so Node, test runners and error reporters print it as an error. Without it, an exception is a plain object and is about 50 times faster to throw, because a native `Error` captures a stack trace when it is created ([#2160](https://github.com/fable-compiler/Fable/issues/2160)); code that throws exceptions for control flow pays that cost on every throw. The flag affects the whole program, including libraries compiled without it. |
| `[<Fickl.Core.JsError>]` on an F# exception | The exception extends a native `Error` subclass, so only it pays for the stack trace. It still matches, compares and prints as an F# exception. |
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

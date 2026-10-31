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
| catching values JavaScript throws | `:? JS.TypeError` and the other `Fickl.Core.JS` error bindings catch JS errors by type, `JS.Thrown` matches a thrown value that is not an error, and `e.Messa... | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#158](https://github.com/shayanhabibi/fickl/issues/158) |
| `--clampByteArrays` | Compiles byte arrays as `Uint8ClampedArray`, the type canvas pixel data uses, so they can be passed to it without a copy. | doesn't compile: Unknown argument: --clampbytearrays | [#39](https://github.com/shayanhabibi/fickl/issues/39) |
| `[<Curried>]` on a parameter | Keeps a function parameter curried, for JavaScript that calls it one argument at a time. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#24](https://github.com/shayanhabibi/fickl/issues/24) |
| `[<Curried>]` on a record field | Keeps a function stored in a record field curried, for a JS object that stores or calls it one argument at a time. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#41](https://github.com/shayanhabibi/fickl/issues/41) |
| `--declaration` | Writes a TypeScript declaration file next to each JavaScript file, so TypeScript can call the compiled F#. | doesn't compile: Unknown argument: --declaration | [#20](https://github.com/shayanhabibi/fickl/issues/20), [#21](https://github.com/shayanhabibi/fickl/issues/21) |
| `[<EmitType>]` | Writes the TypeScript annotation of a type as the given text, for conditional, mapped and template literal types F# cannot describe; the JavaScript is unchan... | doesn't compile: Unknown argument: --declaration | [#145](https://github.com/shayanhabibi/fickl/issues/145) |
| `[<CaseTest>]` on erased union cases | A JavaScript predicate tells apart erased cases with the same payload type, here two strings. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#7](https://github.com/shayanhabibi/fickl/issues/7) |
| an explicit class inheriting another explicit class | The class extends its base and keeps the base's fields, so `{ inherit Base("derived"); Extra = 5 }` has its `Name`. | compiles, but fails at runtime: expected derived, got undefined | [#87](https://github.com/shayanhabibi/fickl/issues/87) |
| the `FICKL_COMPILER` define | Defines `FICKL_COMPILER`, so code can choose Fickl.Core or Fable.Core when both have an attribute. | compiles, but fails at runtime: expected fickl, got fable | [#43](https://github.com/shayanhabibi/fickl/issues/43) |
| `[<InlineInterface>]` | A `TypeScriptTaggedUnion` case holding one interface compiles as that interface with the tag added, `{ type: "take", fromIndex, toIndex }`. | doesn't type-check: The namespace or module 'Fickl' is not defined. | — |
| `[<JsError>]` | An F# exception marked `[<JsError>]` is a JS `Error` subclass with a `stack`, and still matches and compares as an F# exception. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#157](https://github.com/shayanhabibi/fickl/issues/157) |
| `--temporal` | Represents dates and times with the JavaScript Temporal API, a documented flag for Fable's hidden `--test:js-temporal`. | doesn't compile: Unknown argument: --temporal | [#39](https://github.com/shayanhabibi/fickl/issues/39) |
| `keyof<'T>` and `keyof<'T, 'R>` property keys | `keyof _.Port` is the property's JavaScript name, typed `keyof T & string` or `KeysOfType<T, R>` in TypeScript, and `KeyOf.get`/`KeyOf.set` index an object b... | doesn't compile: Unknown argument: --declaration | [#174](https://github.com/shayanhabibi/fickl/issues/174) |
| reading another file's `mutable` value | The read stays before later side effects; it is not inlined past a call that changes the value. | compiles, but fails at runtime: expected 1, got 0 | [#15](https://github.com/shayanhabibi/fickl/issues/15) |
| `--nativeErrors` | Every exception, from `failwith` to a declared F# exception, is a JS `Error` with a `stack`, and an inner exception is its `cause`. | doesn't compile: Unknown argument: --nativeerrors | [#157](https://github.com/shayanhabibi/fickl/issues/157) |
| `[<NoOverloadSuffix>]` | Drops the overload hash from a method's compiled name, `Counter__Add` instead of `Counter__Add_Z524259A4`, so JavaScript can call it by a predictable name. O... | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#35](https://github.com/shayanhabibi/fickl/issues/35) |
| `partial` and `Partial<'T>` | `partial` builds an object with some of a type's properties, typed `Partial<T>` and checked with `satisfies` in TypeScript instead of cast to the full type. | doesn't compile: Unknown argument: --declaration | [#151](https://github.com/shayanhabibi/fickl/issues/151) |
| an explicit class inheriting a class with a primary constructor | The class extends its base, so `{ inherit Base("derived"); Extra = 5 }` has its `Name`. | compiles, but fails at runtime: expected derived, got undefined | [#96](https://github.com/shayanhabibi/fickl/issues/96) |
| read-only collections in TypeScript annotations | `IReadOnlyCollection`, `IReadOnlyList` and `IReadOnlyDictionary` are annotated as types a TypeScript readonly array or `ReadonlyMap` satisfies instead of `an... | doesn't compile: Unknown argument: --declaration | [#146](https://github.com/shayanhabibi/fickl/issues/146) |
| `--strictOptions` | `Some null` of a string, array, class, record or function stays `Some`, as on .NET, built the same way at concrete and generic sites. Value options stay erased. | doesn't compile: Unknown argument: --strictoptions | [#32](https://github.com/shayanhabibi/fickl/issues/32) |
| `[<TsEnum>]` and imported `[<StringEnum>]` unions | Emits a `[<TsEnum>]` enum as the object a TypeScript enum compiles to, typed by an ambient enum in TypeScript, and compiles the cases of an imported or globa... | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#149](https://github.com/shayanhabibi/fickl/issues/149) |
| `Record<'K, 'V>`, `ReadonlyRecord` and `PropertyRecord` | `Fickl.Core.Record<'K, 'V>` is a plain object typed `Record<K, V>` in TypeScript, with `ReadonlyRecord` and `PropertyRecord` for `Readonly<Record<K, V>>` and... | doesn't compile: Unknown argument: --declaration | [#175](https://github.com/shayanhabibi/fickl/issues/175), [#142](https://github.com/shayanhabibi/fickl/issues/142) |
| `unknown`, an abbreviation of `obj` | A module function or abstract member taking or returning `Fickl.Core.unknown` has `unknown` in its TypeScript signature, so TypeScript callers must narrow th... | doesn't compile: Unknown argument: --declaration | [#150](https://github.com/shayanhabibi/fickl/issues/150) |
| `[<TypePredicate>]` and `[<TypeAssertion>]` | Emits a function as a TypeScript type guard, `x is T` or `asserts x is T`, while F# calls it as returning an option or a Result of the narrowed argument. | doesn't type-check: The namespace or module 'Fickl' is not defined. | [#147](https://github.com/shayanhabibi/fickl/issues/147) |
| `valueof<'T>` for `T[keyof T]` | `Fickl.Core.valueof<'T>` is the value of some property of `'T`, typed `T[keyof T]` in TypeScript, built with `ValueOf.ofKey` and read as a type with a visibl... | doesn't compile: Unknown argument: --declaration | [#177](https://github.com/shayanhabibi/fickl/issues/177) |
<!-- fickl-differences:end -->

## Added functionality

What Fickl adds on top of Fable. The computation expressions, attributes and functions live in the `Fickl.Core` package and target JavaScript and TypeScript.

| Feature | Kind | What it does |
| --- | --- | --- |
| `jsAsync { }` | Computation expression | Compiles to a native `async` function returning a `JS.Promise`: `let!` emits `await`, `for` over a `JS.AsyncIterable` emits `for await`, and it starts running immediately. There is no F# cancellation token: pass one to the work with `ToAbortSignal` or `Register`. |
| `ToAbortSignal`, `FromAbortSignal` | Extension members | Convert a `CancellationToken` to the `AbortSignal` that `fetch` and other cancellable JavaScript APIs take, `cts.Token.ToAbortSignal()`, and back, `CancellationToken.FromAbortSignal(request.signal)`. `Async.AwaitPromise(p, token)` stops waiting when the token is cancelled and `Async.StartAsPromise(work, signal)` cancels on abort. An `AbortError` is an `OperationCanceledException` for `:?`, an async awaiting a promise that rejects with one is cancelled, and an `OperationCanceledException` is named `AbortError` for JavaScript. `JS.AbortSignal` and `JS.AbortController` bind the web types. |
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
| `keyof<'T>`, `keyof<'T, 'R>`, `keyof` | Type, function | `keyof _.Port` is the name JavaScript reads the property by, `"Port"`, and `KeyOf.get`/`KeyOf.set` index an object by a key. TypeScript types a key `keyof T & string`, or `KeysOfType<T, R>` for the keys whose value is a `'R`, so `target[key]` is checked. A path, a method or a class property is a compile error. |
| `Record<'K, 'V>`, `ReadonlyRecord<'K, 'V>`, `PropertyRecord<'T, 'V>` | Type | A plain object indexed by its keys, `r.[key]`, typed `Record<K, V>`, `Readonly<Record<K, V>>` and `Record<keyof T & string, V>` in TypeScript. `Record.empty` builds `{}`, `Record.tryGet` finds only own properties and `Record.keys` is `Object.keys`. |
| `valueof<'T>` | Type | The value of some property of `'T`, typed `T[keyof T]` in TypeScript. `ValueOf.ofKey key target` reads one, and `ValueOf.unsafeAs` treats it as a given type with a visible `as` cast. |
| `string<'m>`, `bool<'m>`, `char<'m>` | Type abbreviation | Branded primitives, like `int<'m>` for numbers: `string<UserId>` keeps user IDs apart from other strings in F#. `Brand.tag`, `Brand.untag` and their `Bool` and `Char` variants convert, and compile to nothing. The value is the bare primitive in JavaScript, and TypeScript annotations write `string`, `boolean` and `string`. |
| Read-only collections in TypeScript | Compiler | `IReadOnlyCollection<T>` and `IReadOnlyList<T>` are annotated `Iterable<T>`, and `IReadOnlyDictionary<K, V>` `IReadOnlyMap<K, V>`, instead of `any`, so TypeScript can pass a `readonly T[]` or `ReadonlyMap`. |
| Nullable reference types in TypeScript | Compiler | With `<Nullable>enable</Nullable>`, an F# 9 nullable reference type such as `string \| null` is annotated `string \| null` instead of `string`, so TypeScript code can pass and receive `null`. Fable erases the `\| null`. |
| `Fickl.Core.JS.ReadonlyArray<'T>` | Type | Binds TypeScript's `ReadonlyArray<T>`, for a parameter that accepts `readonly T[]` and `as const` arrays. `ReadonlyArray.ofArray` and `ofResizeArray` view an F# array without a copy. |
| `WeakReference<'T>`, `JS.WeakRef<'T>`, `JS.FinalizationRegistry<'T>` | Runtime, bindings | `WeakReference<'T>` compiles to a native `WeakRef`, typed `WeakRef<T>` in TypeScript, with `TryGetTarget`; `SetTarget` is an error, as a `WeakRef` cannot be retargeted. `JS.FinalizationRegistry` calls back with a held value after a registered object is collected. |
| `unknown` | Type abbreviation | `Fickl.Core.unknown` is `obj` in F#. A module function or abstract member that takes or returns it has `unknown` in its TypeScript signature, so callers must narrow the value, while its body still sees `any`. `obj` stays `any`. |
| `[<TypePredicate>]`, `[<TypeAssertion>]` | Attribute on a function or method | Emits a TypeScript type guard, `x is User` or `asserts x is User`. F# can see the guard as returning `User option`, `User voption` or `Result<User, exn>`: the compiled function still returns a boolean or throws, and F# calls get `Some x` or `Ok x`. Works on imported guards too. |
| `[<TaggedTemplate>]` | Attribute on an imported, global or emitted function | Calls a JavaScript tag with an F# interpolated string as a native tagged template, ``html $"<p>{name}</p>"`` compiling to ``html`<p>${name}</p>` ``, for Lit, GraphQL and SQL tags and `String.raw`. The tag gets the identity-stable strings array and the values unformatted; a format specifier is a compile error. |
| `[<ImportWith>]`, `importDynamicWith` | Attribute on an imported value, function or class, function | Adds import attributes, for JSON and CSS modules: `[<ImportDefault("./config.json"); ImportWith("type", "json")>]` emits `import config from "./config.json" with { type: "json" }`, and `importDynamicWith "./config.json" [ "type", "json" ]` emits `import("./config.json", { with: { type: "json" } })`. TypeScript needs `module` set to `esnext`, `preserve` or `nodenext`. |
| `JS.awaitTopLevel` | Function | `let config: Config = JS.awaitTopLevel (loadConfig ())` emits `export const config = await loadConfig();`, so the value has the resolved type rather than a `Promise`. It is allowed only as the whole initializer of a module-level value; anywhere else it is a compile error. Modules importing this one wait for the promise before they run, and fail to load if it rejects. |
| `[<TsEnum>]` | Attribute on an enum | Emits an F# enum as the frozen object a TypeScript enum compiles to, `Color.Red` and `Color[0]`. TypeScript types it with an ambient `declare enum`, so the output stays erasable, and values of the enum are annotated `Color` instead of `int32`. |
| Imported string enums | Compiler | A case of an imported or global `[<StringEnum>]` union compiles to the member of the enum object, `Dir.Up`, so it type-checks against a TypeScript string enum. |
| `--declaration` | CLI flag | Writes a TypeScript declaration file (`.d.ts`) next to each JavaScript file. |
| `--strictOptions` | CLI flag | Keeps `Some null` apart from `None`, as on .NET. |
| `--clampByteArrays` | CLI flag | Compiles byte arrays as `Uint8ClampedArray`. |
| `ArraySegment<'T>`, `Array.ofArrayBuffer`, `AsArrayBuffer` | Runtime, functions | An `ArraySegment` over a numeric array is a typed-array view, `ArraySegment<byte>(bytes, 4, 16)` compiling to `bytes.subarray(4, 20)`, so it shares the array's memory. `Array.ofArrayBuffer buffer` views a whole `ArrayBuffer` as a numeric array without a copy, and `array.AsArrayBuffer()` gives the array's buffer. `JS.Constructors.BigUint64Array` binds the array `uint64[]` compiles to. |
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
| `[<Fickl.Core.TsEnum>]` on an enum | Emits the enum's object and TypeScript type. Without it, an enum emits nothing and is typed as its underlying number. |
| `[<Import>]` or `[<Global>]` on a `[<StringEnum>]` union | Its cases compile to members of the imported object, `Dir.Up`. Without the attribute, they compile to their string literals. |
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

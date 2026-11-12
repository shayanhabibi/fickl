# Differences from Fable

Each folder is one change Fickl makes that upstream Fable does not have yet. `dotnet run --project src/Fickl.Build -- differences` compiles every case with Fickl and with the latest Fable, runs the output with node, and writes the cases Fable gets wrong into the README table.

- A case fails Fickl's run: the command exits with an error, as that is a regression.
- A case passes with Fable too: it leaves the table, and the command says the folder can be deleted.

## Writing a case

A case is a folder with a `Main.fs`, plus any other `.fs` files, which compile first in name order. `Check.fs` compiles before all of them; `Check.equal expected actual` throws when they differ, which fails the run.

`Main.fs` starts with these comment lines:

```fsharp
// Change: `[<CallableInterface>]`
// Fickl: What Fickl does, in one sentence for the table.
// Issues: 8, 10
// Args: --declaration
```

`Issues` is `none` or Fickl issue numbers. `Args` is optional: extra options for both compilers.

Fickl compiles a case with `src/Fickl.Core` referenced, and Fable with the latest `Fable.Core` package, so a case using a Fickl attribute fails to type-check with Fable.

Fickl defines `FICKL_COMPILER` and Fable does not. When Fable gains an attribute that Fickl has, Fable's version lives in `Fable.Core` rather than `Fickl.Core`, so a case picks the one each compiler has:

```fsharp
#if FICKL_COMPILER
open Fickl.Core
#else
open Fable.Core
#endif
```

Once Fable passes the case, it leaves the table as usual.

## Checked by hand

Some fixes cannot be a case here: the harness compiles one project to JavaScript and runs it, so it cannot type-check TypeScript output, drive watch mode, reference several projects, enable nullable reference types, or keep a case that Fickl rejects at compile time. These were checked by hand against Fable 5.27.0 on 2026-11-12, and still differed. Check them again when Fickl merges a new Fable release.

| Fickl fix | Fable 5.27.0 | Issues |
| --- | --- | --- |
| A multi-targeting project reference is cracked for the framework MSBuild picks (a9c240aba) | doesn't compile: a file built only for `net10.0` is not defined | [#416](https://github.com/shayanhabibi/fickl/issues/416) |
| A project can reference a project that has an `[<EntryPoint>]` (9dca029d2) | doesn't compile: FS0433 | [#300](https://github.com/shayanhabibi/fickl/issues/300), `fable-compiler/Fable#2366` |
| A root-absolute import path such as `/env.js` is emitted as written (e94493e14) | rewrites it relative to the output folder | [#299](https://github.com/shayanhabibi/fickl/issues/299), `fable-compiler/Fable#3146` |
| `DateTime` and other date and time members the runtime does not implement are compile errors (f70596c0a, 13066affb) | compiles `ToOADate` and `ParseExact`; node fails when it loads the module | [#318](https://github.com/shayanhabibi/fickl/issues/318), `fable-compiler/Fable#3767`, `fable-compiler/Fable#2003` |
| A constructor call on an `[<Erase>]` class is a compile error (e0e144374) | compiles; node fails with a `ReferenceError` | [#316](https://github.com/shayanhabibi/fickl/issues/316), `fable-compiler/Fable#3851` |
| Watch mode keeps running when a source file is briefly locked (c36efc953) | logs the sharing violation and exits with code 1 | [#296](https://github.com/shayanhabibi/fickl/issues/296), `fable-compiler/Fable#4080` |
| A deleted output file is regenerated on the next compilation (1c8d29b7e) | "Skipped compilation because all generated files are up-to-date!", and the file stays missing | [#294](https://github.com/shayanhabibi/fickl/issues/294), `fable-compiler/Fable#5229` |
| A run after a failed compilation compiles again (bdcef7c57) | the second run is skipped as up to date | none |
| Union and record equality survive a hot reload (40f5abf33) | values of the same type from two loads of a module are not equal | [#313](https://github.com/shayanhabibi/fickl/issues/313), `fable-compiler/Fable#3102` |
| The project's `LangVersion` is enforced (9f84f562d) | ignores it: F# 9 code compiles under `LangVersion` 8.0 | [#255](https://github.com/shayanhabibi/fickl/issues/255) |
| An undefined name caused by an assembly-level `AutoOpen` gets a hint (8b013a42c) | FS0039 with no hint | [#279](https://github.com/shayanhabibi/fickl/issues/279), `fable-compiler/Fable#3285` |
| A null `string \| null` operand concatenates as empty (8f9062f80, needs `<Nullable>enable</Nullable>`) | prints `null` | [#272](https://github.com/shayanhabibi/fickl/issues/272), `fable-compiler/Fable#5162` |
| TypeScript: an SRTP call to a generic static member gets valid type arguments (b7c710977) | `tsc --strict`: TS2558 and TS2304 | [#323](https://github.com/shayanhabibi/fickl/issues/323), `fable-compiler/Fable#3921` |
| TypeScript: dynamic access, `unbox` and `!!` are cast (b7076ab3d) | `tsc --strict`: TS2339 and TS2741 | [#199](https://github.com/shayanhabibi/fickl/issues/199) |
| TypeScript: `IDictionary<'K, 'V>` is annotated `IMap<K, V>` (11916d765) | annotated `any`; `tsc --strict` reports TS2571 | [#283](https://github.com/shayanhabibi/fickl/issues/283), `fable-compiler/Fable#3665` |
| TypeScript: the static singletons of fieldless union cases are typed (a87762bc7) | typed `any` (compiles, weaker type) | [#281](https://github.com/shayanhabibi/fickl/issues/281), `fable-compiler/Fable#5219` |
| TypeScript: a JSX template with one interpolated value compiles (f99ac91e1) | doesn't compile: "Expecting a string literal or interpolation without formatting" | [#417](https://github.com/shayanhabibi/fickl/issues/417) |

Not checked: the target language is kept per compilation rather than per process (d3b65725a, [#398](https://github.com/shayanhabibi/fickl/issues/398)). The bug needs two compilations for different languages in one process, which a single Fable CLI run does not do.

Already fixed in Fable 5.27.0, so no case: the recursive trait call that ends in an overload taking `obj` (fe3e215f8, [#288](https://github.com/shayanhabibi/fickl/issues/288)), and `for i in a .. step .. b` as a counted loop (c25f07fce, [#317](https://github.com/shayanhabibi/fickl/issues/317)), where Fable's output was already correct.

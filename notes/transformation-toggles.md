# Transformation toggles (inventory, 2026-10-25)

The README asks that every implicit transformation have an off switch that the compiler always honours, including
across generic boundaries. This is a first inventory for JavaScript/TypeScript, from reading the source. Nothing in it
has been confirmed with a test yet. Paths are under `src/Fable.Transforms/` unless they start with `src/`.

| Behaviour | Where | Toggle | Gap |
| --- | --- | --- | --- |
| Beta reduction / let inlining | FableTransforms.fs `canInlineArg`, `tryInlineBinding`, `bindingBetaReduction` | `-c Debug` keeps user bindings | No per-binding or per-member switch; inlined-call arguments are compiler-generated (FSharp2Fable.fs `ResolveInlineExpr`) so Debug still erases them |
| F# `inline` expansion | FSharp2Fable.fs `ResolveInlineExpr` | none | No way to emit an inline member as a call |
| `[<InlineIfLambda>]` | FSharp2Fable.fs (`CurriedParameterGroups`) | the attribute | Read only from the member itself, so not through interface/abstract dispatch; lambdas are beta-reduced without it anyway |
| Uncurrying / arity | FableTransforms.fs uncurry passes | `System.Func`/delegates; imports | No attribute keeps a curried shape; arity mismatch unhandled (`TODO`), generic returns fall back to runtime curry/uncurry; lambdas passed to lambdas never uncurried |
| Auto-uncurrying of `Emit` args typed `obj` | FableTransforms.fs | none | No per-Emit opt-out |
| Tail call → loop | Babel/Fable2Babel.fs | none | Every self-tail-recursive function is rewritten |
| Option erasure | Transforms.Util.fs `mustWrapOption`, Fable2Babel.fs | none | Representation differs between generic and concrete construction sites |
| Overload-suffix mangling | FSharp2Fable.Util.fs, OverloadSuffix.fs | `[<Mangle(false)>]` (statics only), `[<AttachMembers>]` | No toggle for instance members; hash added even when not overloaded |
| Interface member mangling | FSharp2Fable.Util.fs | `[<Mangle>]`, AttachMembers/Global/Import/Pojo | `System.*` interfaces mangled outside a whitelist; **TS declarations re-implement it differently** (Fable2Babel.fs `TODO: replicate the mangling logic`, checks `Mangle` presence not value) |
| Members as module functions vs attached | FSharp2Fable.fs | `[<AttachMembers>]` | All or nothing per type |
| Union representation | Fable2Babel.fs | Erase, StringEnum, TypeScriptTaggedUnion, CaseTest, CompiledValue, InlineInterface, CompiledName | Erased/StringEnum unions can't implement abstract members |
| Records as `Types.Record` with structural equality | Fable2Babel.fs, fable-library-ts/Types.ts | `JS.Pojo` (classes only) | No POJO toggle for records; **`[<ReferenceEquality>]`/`[<NoEquality>]` may not be honoured** (equality goes to `Util.equals` for every declared type, Replacements.fs); test type `RTest2` in ComparisonTests.fs is never asserted |
| Reflection info | Fable2Babel.fs | `--noReflection` | Global only |
| Typed arrays for numeric arrays | Replacements.fs | `--typedArrays` | Decided by the static element type, so `'T[]` built generically differs from `int[]`; `ClampByteArrays` has no CLI flag |
| int64/decimal/char/DateTime representation | Replacements.fs | Temporal only via undocumented `--test:js-temporal` | none |
| ParamArray spread | FSharp2Fable.Util.fs | `[<ParamList>]` for seq | No opt-out |
| `[<ParamObject>]` | FSharp2Fable.Util.fs | the attribute | Must be on the abstract slot to apply through an interface |

## Most significant gaps

1. `[<ReferenceEquality>]`/`[<NoEquality>]` possibly ignored in JS: confirm with a runtime test.
2. TS declaration mangling diverges from the JS mangling, so `--declaration` output can name members differently.
3. No toggle for uncurrying/arity, and it breaks at generic boundaries.
4. No opt-out of overload-suffix mangling for instance members.
5. No off switch for beta reduction, F# inline expansion or tail-call rewriting.
6. Representation depends on genericity at the use site (option wrapping, typed arrays), with no attribute to pin it.
7. Hidden or unreachable options (`ClampByteArrays`, `--test:js-temporal`).

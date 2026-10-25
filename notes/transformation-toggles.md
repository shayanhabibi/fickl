# Escape hatches for correctness rewrites (inventory, 2026-10-25)

Fickl quietly rewrites F# so that it keeps its .NET meaning in JavaScript: it uncurries functions, wraps options,
mangles names, turns tail calls into loops. Each rewrite is there to make the output correct, but the compiler can
apply one where it is wrong, and then the user needs a way out. The direction is that every such rewrite has an
escape hatch (an attribute or a flag) that the compiler always honours, including across generic boundaries.

Optimisations that cannot change behaviour, such as beta reduction or reflection info, are out of scope. If one of
them does change behaviour (reordering or duplicating side effects), that is a bug to fix, not a toggle to add.

This inventory covers JavaScript/TypeScript and comes from reading the source; rows are unconfirmed unless they cite a
test. Paths are under `src/Fable.Transforms/` unless they start with `src/`.

## Rewrites that need an escape hatch

| Rewrite | Why it exists | How it misfires | Escape hatch today |
| --- | --- | --- | --- |
| Uncurrying / arity (FableTransforms.fs uncurry passes) | Match the JS calling convention | JS that calls a callback one argument at a time, or passes a curried function in (#24). Pure F# round trips agree with .NET, including effects between partial applications (tests/Js/Main/CurryingTests.fs) | `[<Fickl.Core.Curried>]` on parameters, honoured on the slot a member implements; not yet on record or union fields |
| Option erasure and `Some` wrapping (Transforms.Util.fs `mustWrapOption`, Fable2Babel.fs) | Keep `Some None` and `Some null` apart from `None` | Wrapping depends on whether the construction site is generic, so one value can be built two ways | none |
| Instance-member overload mangling (FSharp2Fable.Util.fs, OverloadSuffix.fs) | JS has no overloading | Names are unpredictable for interop; the hash is added even without an overload | `[<Mangle(false)>]` covers statics only; `[<AttachMembers>]` |
| `System.*` interface member mangling (FSharp2Fable.Util.fs) | Avoid name collisions | Interfaces outside a whitelist are mangled, breaking JS that implements them by name | `[<Mangle>]` and friends on user interfaces only |
| Tail call → loop (Babel/Fable2Babel.fs) | Avoid stack overflow | Suspected: closures captured in the loop body share variables. Not reproduced: parameters, lets, mutables, tuples, guards, methods, object expressions, records, lazies and chained closures all keep their iteration's values (TailCallTests.fs) | none needed so far |
| Auto-uncurrying of `Emit` arguments typed `obj` (FableTransforms.fs) | Make an F# lambda callable from emitted JS | Also uncurries when the JS side expects a curried function | `[<Fickl.Core.Curried>]` on the parameter |
| `ParamArray` spread (FSharp2Fable.Util.fs) | Match JS variadic calls | Wrong when the JS API takes an actual array | `[<ParamList>]` for seq only |
| Typed arrays and byte clamping (Replacements.fs) | Match .NET numeric wraparound | Chosen by the static element type, so `'T[]` built generically differs from `int[]` | `--typedArrays`; `ClampByteArrays` has no CLI flag |
| `[<ParamObject>]` (FSharp2Fable.Util.fs) | Interop with options objects | Applies through an interface only when the attribute is on the abstract slot | the attribute |
| `[<InlineIfLambda>]` (FSharp2Fable.fs) | Avoid closure allocation | Read only from the member itself, so not through interface/abstract dispatch | the attribute |

## Representation choices with an existing switch

| Behaviour | Switch | Gap |
| --- | --- | --- |
| Union representation | Erase, StringEnum, TypeScriptTaggedUnion, CaseTest, CompiledValue, InlineInterface, CompiledName | Erased/StringEnum unions can't implement abstract members |
| Records as `Types.Record` | `JS.Pojo` (classes only) | No POJO switch for records |
| int64/decimal/char/DateTime | Temporal only via undocumented `--test:js-temporal` | Hidden |
| Members as module functions vs attached | `[<AttachMembers>]` | All or nothing per type |

## Bugs, not toggles

- `[<ReferenceEquality>]`/`[<NoEquality>]` may be ignored: equality goes to `Util.equals` for every declared type
  (Replacements.fs), and test type `RTest2` in ComparisonTests.fs is never asserted. Needs a runtime test.
- TS declaration mangling used to diverge from JS mangling; fixed in #20.

## Order of work

1. Uncurrying / arity: `[<Curried>]` on parameters is done; record and union fields remain, which needs field
   attributes that the Fable AST does not carry.
2. Option wrapping: pin the representation regardless of genericity.
3. Instance-member overload mangling opt-out.
4. Tail-call rewrite opt-out: the closure-capture misfire did not reproduce in 13 shapes, so no opt-out yet.
5. `ParamArray` opt-out.
6. Surface `ClampByteArrays` and Temporal as documented flags.

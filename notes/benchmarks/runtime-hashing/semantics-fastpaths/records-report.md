# Experiment 2: compiler-guaranteed record equality

Started after options-report.md was written. No production edits, commits or rollout.

Do not replace immutable record structural equality with reference equality. Two distinct `{ N=1; S="a" }` allocations compare equal on .NET and JS. Immutability does not imply canonical allocation. A negative identity result cannot answer structural equality unless a separate, enforced interning scheme guarantees canonical representatives.

## Concrete results

`Records.fsx` ran with dotnet fsi under an exclusive slot. The definitive clean rerun exited **0**, recorded in `records-dotnet-results.json` by `run-dotnet.py`. `records-dotnet-results.txt` also retains the earlier noisy PowerShell capture for provenance. `records.mjs` ran against the existing generated runtime and wrote `records-results.json`.

| Case | .NET F# `=` | Existing JS Util.equals |
|---|---|---|
| Distinct equal int/string records | true | true |
| Same record containing NaN | false | true |
| Distinct records containing NaN | false | false |
| Same custom-equality record whose Equals returns false | false, one Equals call | true, zero Equals calls |
| Same outer record containing the custom record | false, one nested Equals call | true, zero nested Equals calls |
| Distinct records containing equal arrays | true | true |
| Same distinct records after mutating one array | false | false |

.NET direct `n.Equals n` for the NaN record returns true, whereas F# `n=n` returns false. Thus equality flavor matters; the existing JS identity shortcut already differs from F# generic equality in these examples. This experiment reports those discrepancies rather than blessing them as the semantic specification. Direct JS custom `.Equals` returns false, confirming the skipped call is observable. No passing .NET test was modified.

## Existing paths and metadata

`Util.ts:530` returns true on identity before custom Equals dispatch; `Types.ts:130` independently returns true on record identity. Adding another identity shortcut does not remove structural traversal work that currently occurs on identical records. `equalArraysWith` traverses elements; anonymous-object equality sorts keys and checks fields. Python's record helper also has an identity shortcut (`record.py:12`); this was inspected, not executed.

Compiler `Replacements.fs:690` routes declared-type equality through Util.equals. Babel code generation derives records from runtime Record (`Babel/Fable2Babel.fs:4314`). Entity metadata already exposes IsFSharpRecord, FSharpFields, field.IsMutable, field types and attributes. `FSharp2Fable.Util.hasStructuralEquality` is not proof that custom equality is absent: it recognizes record shape and attributes, so a new eligibility predicate must inspect custom equality/overrides explicitly. Existing `HasReferenceEquality` metadata is about semantic equality category, not deep immutability or interning. No AST change is necessary merely to inspect existing entity information.

An immutable record field holding an array, ref cell, mutable object or custom-equality value is only shallowly immutable. The harness demonstrates equality and hash changing after array mutation without assigning the record field. Even deep immutability alone does not prove reflexivity: NaN remains nonreflexive under F# `=`. Custom equality can also observe calls, throw, or violate reflexivity regardless of field mutation. Unknown generic fields and JS interop invalidate broad static guarantees.

## Safe scoped candidate

The original runnable JS harness models compiler-generated direct-field comparison for a closed ordinary record with int and string fields, no overrides, subclassing, added fields, proxies or JS mutation. It preserves the identity-positive path and compares fields on different instances. All **1,060 original checks** pass, including null/undefined **record operands**, equal/unequal fresh instances, cross-record RHS and subclass rejection. Those original cases used nonnull primitive strings in fields: they did not prove nullable-field coverage. A nominal prototype check guards the RHS; the nonnull LHS has the statically guaranteed Plain type. Instrumented getters confirm the same `a.N,b.N,a.S,b.S` read order; arbitrary getter-bearing shapes remain outside eligibility.

### What would actually change

For this F# declaration:

```fsharp
type Plain = { N: int; S: string }
```

The existing typed equality call reaches `Util.equals`, then inherited `Record.Equals`, then `recordEquals`. The latter discovers field names with `Object.keys` and invokes generic equality on each field. FCS already knows both the field names and their types; the candidate replaces that discovery and generic dispatch with fixed field operations. It does **not** change the class representation, constructor, fields, reflection, copying, hashing, or comparison ordering.

A conservative intended helper for a proven Plain left operand is:

```javascript
function Plain_equals(left, right) {
    if (left === right) return true;
    if (left == null) return right == null;
    if (right == null || Object.getPrototypeOf(right) !== Plain.prototype) {
        return false;
    }
    if (left.N !== right.N) return false;
    const leftString = left.S;
    const rightString = right.S;
    return leftString === rightString
        || (leftString == null && rightString == null);
}
```

`===` is sufficient for valid int32 fields. The string operation additionally preserves the current runtime's equivalence between null and undefined; this is a JS representation detail, not a new F# value. A future compiler could omit the RHS prototype test when it also proves exact runtime provenance for both operands, but **that omission was not measured or validated here**. For general untyped callers, the LHS would need a corresponding guard or fallback; the helper is not an unrestricted JS structural-equality function.

The string locals retain one read of each field and left-before-right order. An unequal N short-circuits before reading S. For a call such as `produceLeft() = produceRight()`, generated temporaries must evaluate both operand expressions once, left to right, **before** entering this helper; field short-circuiting must not skip the right operand expression. The same helper can serve `<>` by negating its result after both operands have been evaluated.

### Exact initial eligibility

The first implementation should deliberately recognize a small closed domain:

1. The operation is compiler-recognized F# structural equality for an ordinary declared record, rather than a custom comparer invocation, arbitrary `.Equals` call, or reference-equality operation.
2. FCS identifies the record, ordered instance fields and substituted concrete field types. Ordinary generated equality must be established explicitly: exclude CustomEquality/ReferenceEquality/NoEquality, user Equals implementations, and interop/erasure attributes that alter layout or dispatch. Record shape alone is insufficient.
3. Begin with valid int32 and string fields, the exact tested shape. Strings may be null; type `string` alone does not imply nonnull. Bool and char are plausible extensions, but were not part of this direct-field comparator experiment. Int64/decimal/bigint and other built-ins need their actual target representations/replacements checked rather than treating every numeric FCS type as a JS number.
4. Exclude floats initially. A later range/value analysis could prove non-NaN for particular float expressions, but FCS's float type and an equality constraint alone do not supply that proof. Unknown generic fields require a fallback; closed generic instantiations may be eligible only after substitution and the same field checks.
5. Exclude nested records, options, lists, arrays, obj/interface fields and recursive types from the **first direct-field generator**. Recursively specialized nested records are a natural next step, with a visited-type analysis and preserved null/nominal/equality flavor at every level. The separate callsite-identity experiment already models a safe nested shape; that is not validation of a generalized field-comparator generator.
6. Establish ordinary generated field storage and provenance: no imported class, proxy, user accessor, replaced prototype, extra own fields, or instance override. FCS metadata identifies the F# layout, but does not by itself establish that a value crossing arbitrary JS interop still honors it. Use the existing path where that guarantee is unavailable. A production design must state which trusted-F# assumptions are already part of the compiler contract and where an additional escape/provenance analysis is required.

Readonly fields are not the reason this works. Mutable primitive fields can still be compared correctly by reading their current values in the original order; they neither permit equality-result caching nor invalidate a positive identity test with pure/reflexive field equality. Initially excluding mutable records can simplify implementation review, but it is a scope choice rather than a semantic necessity. Conversely, readonly reference fields do not make their referents immutable or their equality pure.

### Expanded null-field checks

The follow-up [records-scoped.mjs](records-scoped.mjs) adds **3,984 passing checks**: int32 boundaries, signed zero, null/undefined/empty/Unicode string fields, nullish record operands, foreign record types, randomized equal/unequal records, and field-read/short-circuit traces. [records-scoped-results.json](records-scoped-results.json) records the results.

It found a concrete limitation in the original measured comparator: `Plain(1,null)` versus `Plain(1,undefined)` is true under baseline runtime equality but false with strict S-field comparison. The expanded helper above returns true. Ordinary F# exposes null rather than a distinct undefined string value, but the original benchmark should not be described as proof for arbitrary JS representations of nullable fields. No expected test was weakened. This closes a baseline compatibility gap in the **modeled** helper; it does not claim a compiler implementation or fresh timing of that expanded helper. No undefined-field behavior was included in the original timing workload.

An exclusive paired timing run (`records-timing.mjs`, raw `records-timing-results.json`) compared actual inherited Record.Equals against the direct comparator. Nine alternating ABBA/BAAB blocks, 25ms per sample after 50ms per-function warmup, Node v26.7.0: median time changes **-57.7% equal distinct, -55.4% unequal first field, -64.3% unequal last field, -3.8% same object**. These are descriptive microbenchmark results, not confidence intervals or application gains. An initial method-only null case threw in baseline Record.Equals(null) and was removed from method-only timing; null remains covered against public Util.equals in correctness. Thus this candidate models a statically typed F# equality callsite with a null guard, not a drop-in behavioral replacement for every direct JS `.Equals` invocation. No compiler implementation exists.

The large gains are specifically from avoiding runtime field-name enumeration and generic field comparisons for distinct records. Identity was already cheap, hence the much smaller same-object result. These measurements used prebuilt objects and cheap fields; construction, hashing, large graphs, real applications, code-size costs and other JS engines were not measured. A production compiler integration should compare the complete current emitted `Util.equals` callsite with the complete generated-helper callsite, not present this method microbenchmark as that end-to-end measurement.

### Where to implement and who benefits

The smallest initial integration is a compiler-generated helper per eligible record/type instantiation, called from the existing declared-type equality replacement in `Replacements.fs`. Reuse the compiler's field-name mapping and existing JS AST nodes. One helper per type avoids repeating a large comparator at every callsite. No public Fable.AST shape change is proposed. Whether helper ownership, accessibility and cross-module reuse need new internal plumbing still requires a prototype.

This first approach accelerates recognized typed `=`/`<>` callsites. It would **not automatically accelerate** a generic Dictionary/HashSet comparer, equality through obj, unknown generic code, or the record's inherited `.Equals`; those can continue through the runtime. Generating a specialized Equals method could broaden benefits, but it has a larger dispatch/equality-flavor/null-contract surface and must be a separate integration decision. The experiment is not permission to override user Equals implementations or bypass an explicit IEqualityComparer.

Keep GetHashCode unchanged initially: preserving the same equality relation preserves the existing equal-values/equal-hashes contract. Direct-field hashing and comparison are separate optimizations with their own rules; neither follows automatically from faster equality. Generated code size, generic-instantiation duplication, bundle/tree-shaking behavior and fallback coverage must be measured alongside runtime speed.

Before calling the implementation ready, compile actual F# fixtures with the new compiler and compare unchanged .NET expectations: fresh equal/unequal allocations, null operands and fields, int boundaries, exactly-once operand effects, field order, cross-module/closed-generic use, custom-equality rejection and unknown/interop fallback. Add NaN/custom-equality counterexamples as **eligibility rejection tests**, not passing cases that bless existing JS discrepancies. Then run the JS/TS suites and representative workloads. The current evidence establishes a worthwhile narrowly scoped candidate, not a completed production optimization.

Suggested next work: prototype generation of field-wise equality for a deliberately narrow proven shape, and benchmark actual call-site dispatch and object-size tradeoffs. Exclude custom equality and floats until the exact F# equality flavor is represented. Retain runtime fallback for unknown/generic/interop shapes. Guarantee field comparison order and exactly-once operand evaluation. A generalized identity-positive optimization requires reflexive, pure equality of every reachable field, not merely readonly fields; existing runtime shortcuts mean the payoff may be negligible. Cached hashes require stronger deep immutability and interop guarantees and were not implemented.

Blast radius: compiler-generated methods alter per-type code size and dispatch, and must preserve custom implementations, null handling, generic instantiation, inherited methods, mutable records and anonymous records. Changes to Util.equals would affect nearly every structural collection/type and expose existing NaN/custom discrepancies. Keep that correctness problem separate from performance rollout. No full JS/TS suite, fresh compiler generation, browser coverage or end-to-end application speedup is established here.

## FCS-backed positive-identity callsite specialization

Following user clarification, the second record subexperiment directly models the proposed compiler guarantee: **if identity is equal, return true; otherwise retain structural equality**. This is valid when the compiler proves the equality operation is reflexive and free of user-observable effects for the entire reachable type. Mutability alone does not invalidate this positive shortcut: a mutable int field is still equal to itself in ordinary synchronous F# execution. Deep immutability is relevant to hash caching, not a prerequisite for this narrower identity test.

Conservative eligibility can use existing FCS-backed metadata: `FsEnt.Attributes` (`FSharp2Fable.Util.fs:469`), `FSharpFields` (503), `IsFSharpRecord` (520), `FsField.IsMutable` (77), and member `IsOverrideOrExplicitInterfaceImplementation` (329). Permit ordinary generated record equality with recursively proven bool/int/char/string fields and nested eligible records; reject CustomEqualityAttribute, user equality implementations, floats capable of NaN, unknown generic parameters, Any/obj, imported/erased/interop shapes and custom comparers. Resolve substituted generic field types and use a visited-type analysis; conservatively reject recursive cycles until their semantics are justified. An `equality` generic constraint alone does not establish purity/reflexivity. Initially reject containers with custom dispatch rather than assuming every reachable type is safe. Readonly is neither sufficient nor necessary. A dedicated compiler utility predicate, rather than altering AST definitions, can express this proof.

Intended lowering in Replacements.fs's declared-type equality route: evaluate each operand once, in source order, into compiler-generated temporaries; then emit `left === right || Util.equals(left,right)`. Already stable identifiers need no additional evaluation. This retains nominal checks and existing fallback on unequal references. It changes neither representation nor hash contract. Do not rewrite `f() === g() || equals(f(),g())`, which duplicates effects and exceptions. Restrict the transformation to the proven F# equality operation, not arbitrary `.Equals`, custom comparer calls or erasure-driven JS values. F# record/value representations and equality flavors must remain explicit.

`callsite.mjs` supplies **68** correctness checks across distinct/same records, recursively safe nested records, null/undefined, cross nominal types, mutation, and effectful/throwing left/right operand producers evaluated once. Raw correctness is in `callsite-results.json`. The production compiler predicate/emission is source-backed design only; the harness represents the intended emitted JS and does not claim FCS implementation.

An exclusive nine-block ABBA/BAAB timing run compares current `Util.equals(a,b)` with `a===b || Util.equals(a,b)` for already-evaluated operands. Raw samples/checksums are in `callsite-timing-results.json`. Median time changes: **same -5.5%, nested same -3.8%, mixed +2.7%, distinct equal +2.6%, distinct unequal +2.8%, nested distinct +3.0%**. The existing runtime already handles identity; the possible gain is avoiding dispatch, not newly avoiding recursive traversal. The extra branch costs time on distinct/mixed workloads here. One Node run is insufficient for rollout; prioritize the stronger direct-field opportunity or representative same-object-heavy applications. No full application/compiler speedup is established.

F# structs are a separate representation/escape-analysis question: ordinary JS has no universal F#-style user value-type storage/copy semantics. A compiler could scalar-replace nonescaping proven local records/structs or specialize storage, but that requires preserving copies, mutation, boxing, aliasing and escape behavior. This experiment does not implement scalar replacement or equate `[<Struct>]` with automatic allocation elimination.

Reproduce from the Fickl worktree: `rtk proxy python notes/benchmarks/runtime-hashing/semantics-fastpaths/run-dotnet.py`, then `rtk node notes/benchmarks/runtime-hashing/semantics-fastpaths/records.mjs`, `rtk node notes/benchmarks/runtime-hashing/semantics-fastpaths/records-scoped.mjs`, `rtk node notes/benchmarks/runtime-hashing/semantics-fastpaths/records-timing.mjs`, and `rtk node notes/benchmarks/runtime-hashing/semantics-fastpaths/callsite.mjs --timing`. Serialize dotnet/build and benchmark work as requested. Runtime prerequisites match the parent README.

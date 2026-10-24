# Ordered Map/Set: lazy per-wrapper Count cache

Completed first, 2026-10-23. **Reject as an unrestricted replacement; potentially useful for compiler-proven non-escaping immutable F# collections.** No production files changed. This experiment is complete before starting the hash-collection experiment.

## Suggested change and evidence

`src/fable-library-ts/Map.fs:825` computes `MapTree.size tree` on every Count; `Set.fs:763` computes `SetTree.count s.Tree`. Both traverse the entire balanced tree. Add `let mutable countCache = -1` to each wrapper and implement Count as `if countCache < 0 then countCache <- <existing count>; countCache`. Every persistent update constructs a fresh wrapper with cache -1; tree nodes remain untouched. The isolated generated JS overlay models precisely that field initialization and conditional, but was not generated from modified F#; compiling the proposed source is a remaining validation step. The Dart Map source also traverses on Count; no cross-target change was attempted.

## Experiment and results

Actual existing generated runtime modules are copied to isolated baseline/candidate directories by `prepare.py ordered`. Exact source and generated input hashes are in `ordered-manifest.json`; no substitute tree implementation is benchmarked. Node v26.7.0, Windows. Nine paired ABBA/BAAB blocks per workload/size, two samples per variant/block, calibration around 12 ms on baseline with a 2 million iteration cap, 10,000 warmup calls, explicit GC between blocks. Raw samples/checksums: `ordered-results.json`. Exclusive benchmark slot was coordinated with root; ordinary workstation noise remains possible.

Median elapsed-time percentage change (negative is faster):

| Collection / size | Repeated Count | Fresh wrapper + first Count | Add + Count | Add only |
|---|---:|---:|---:|---:|
| Map / 0 | -18.9% | +1.6% | -6.0% | +2.2% |
| Map / 1 | -47.7% | -9.8% | -10.1% | -2.8% |
| Map / 32 | -97.5% | -46.3% | -23.0% | +5.0% |
| Map / 1024 | -99.93% | -48.2% | -44.9% | -4.0% |
| Set / 0 | -20.8% | +29.8% | +7.9% | -0.8% |
| Set / 1 | -46.9% | +19.1% | -0.3% | -2.5% |
| Set / 32 | -97.2% | -30.6% | -17.9% | +5.9% |
| Set / 1024 | -99.90% | -29.3% | -27.4% | -1.5% |

Repeated reads change from O(n) to O(1) after one traversal. Cold reads still traverse, so their unexpectedly large apparent gains at larger sizes are **not an algorithmic result**: duplicated modules/object layouts/JIT specialization are plausible confounders. Do not interpret those figures as established cold-path improvements. Empty/singleton Set cold regressions and the extra field on every wrapper count against blanket deployment. No heap/GC bytes or application speedup measured; no statistical confidence intervals or browser-engine checks.

## F# semantics and counterexamples

`ordered.mjs --check-only` records `ordered-correctness.json`: 300 persistent updates each for Map/Set, exact descending comparer traces (1,139 / 1,182 calls), old-version contents and root identity, repeated Count without comparer calls, sorted enumeration continuity across Count, and throwing comparers on singleton Count. Both variants match. No equality/hash invocation is added; tree updates/ordering algorithms are unchanged. The JS interfaces expose the same Count path. These are runtime differential checks, not a compiled F#/.NET conformance suite.

The guarantee required is stronger than merely an F# static type: wrapper/tree representation must remain logically immutable and not be frozen or inspected through JS interop. Concrete recorded counterexamples for **both** Map and Set:

* `Object.freeze(collection); count(collection)` returns 1 on baseline, throws TypeError on candidate (lazy write).
* `count(a); a.tree = twoElementTree; count(a)` returns 2 on baseline, stale 1 on candidate.
* `Object.keys` adds `countCache`; consequently `Object.values`, spread, and property descriptors observe a changed representation.

FSharpMap/FSharpSet custom `GetHashCode`, equality, comparison and `toJSON` enumerate contents rather than object fields, so their F# behavior is not intrinsically affected by the extra field. Generic JS reflection remains affected. No compiler non-escape guarantee was established. A module-private WeakMap could avoid own-field/freeze changes, but still requires immutable trees, changes storage/lookup costs, and **was not benchmarked**; it is a separate candidate, not evidence of safety for this one. Compiler-known counts or immutable subtree-size metadata established at node construction are preferable follow-up designs to investigate: they avoid lazy wrapper mutation, but require construction/rotation auditing and their own memory/cost measurement. Neither was measured here. Never mutate shared tree nodes to cache counts without an independent review.

## Blast radius and reproduction

Touches two wrapper constructors and Count implementations; all JS/TS Map/Set counts and `.size` calls use these, and every wrapper pays field storage even without Count. Source-linked targets would need project-file audit and their own validation before rollout. No AST/public signature changes proposed. Native JS Map/Set are unaffected. Existing iterators retain tree snapshots and count reads do not mutate those trees.

From main repository root, sequentially:

```powershell
rtk proxy python .worktrees/fickle/notes/benchmarks/runtime-hashing/keyed-collections/prepare.py ordered
rtk node .worktrees/fickle/notes/benchmarks/runtime-hashing/keyed-collections/ordered.mjs --check-only
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/keyed-collections/ordered.mjs
```

The current generated runtime is a prerequisite; the manifest records the inputs actually tested. Timing JSON preceded addition of interop counterexample output to the harness; correctness JSON contains those later counterexamples. The benchmark logic was unchanged.

# Dictionary/HashSet: one native bucket lookup

Second experiment, started only after `ordered-report.md` was completed, 2026-10-23. **Promising local optimization; recommend source implementation and full validation before rollout.** Two independent runs improved all tested cases. No production sources changed.

## Suggested change

`src/fable-library-ts/MutableMap.fs` and `MutableSet.fs`, private `TryFindIndex`, call `hashMap.TryGetValue h`. Actual generated JS allocates an out-reference with closures, calls `MapUtil.tryGetValue` (`has`, then `get` on hit), builds a temporary pair, then searches the bucket. Replace only this private lookup with one native `get` and an undefined guard:

```javascript
const h = this$.comparer.GetHashCode(k) | 0;
const bucket = this$.hashMap.get(h);
if (bucket !== undefined) {
    return [true, h, bucket.findIndex(pair => this$.comparer.Equals(k, pair[0]))];
}
return [false, h, -1];
```

HashSet uses the element itself rather than `pair[0]`. Keep the result triple, the existing `findIndex`, and all later bucket re-fetches. Suggested F# implementation is a narrowly scoped native helper (for example an Emit of `$0.get($1)` returning an option of a bucket array) used only here. The helper must document that buckets are never null/undefined. If implementing via an option helper, inspect the compiled output to ensure it preserves the shown code and does not reintroduce wrappers. The experiment changes generated JS directly and has **not** compiled that proposed F# helper.

Do not change generic `MapUtil.tryGetValue`: ordinary native dictionaries may legitimately hold undefined as a value. This candidate relies on the stronger private invariant: every value in `hashMap` is an allocated bucket array, even when user values/keys are null-like. Missing and undefined can therefore coincide only if JS interop corrupts the private representation. Monkey-patched native Map methods can observe fewer `has` calls and are outside the asserted semantics. Hash/comparison callbacks are not excluded.

## Experiment and measurements

`prepare.py hash` copies the real generated runtime, then patches only the two private helpers; input hashes are in `hash-manifest.json`, exact overlays in `MutableMap.generated.patch` / `MutableSet.generated.patch`. No synthetic Dictionary implementation is substituted. Native primitive dictionaries are not benchmarked as a proxy for structural-key paths.

Node v26.7.0 on Windows; 11 ABBA/BAAB paired blocks per case, 20,000 warmup calls, approximately 12 ms calibrated baseline samples (maximum 2 million iterations), explicit GC per block, matching checksums. Two fresh-process runs used an exclusive timing slot. Results are descriptive median paired elapsed-time percentage changes; negative is faster. Ordinary workstation noise remains possible, and no confidence intervals are claimed.

| Kind / distribution | Hit (run / repeat) | Miss | Existing update | Remove + add |
|---|---:|---:|---:|---:|
| Dictionary / 1,024 distinct hashes | -41.2 / -37.2% | -26.3 / -17.9% | -39.2 / -34.7% | -37.2 / -36.6% |
| Dictionary / 16 fully colliding keys | -19.6 / -21.0% | -26.7 / -18.4% | -31.8 / -41.4% | -22.4 / -29.6% |
| HashSet / 1,024 distinct hashes | -40.5 / -47.5% | -19.9 / -31.2% | -40.1 / -43.7% | -23.8 / -42.6% |
| HashSet / 16 fully colliding keys | -22.0 / -26.6% | -24.2 / -23.1% | -31.0 / -35.2% | -29.7 / -28.5% |

HashSet existing update means duplicate Add. Raw blocks, ns/op, iteration counts, checksums, and behavior outcomes: `hash-results.json`, `hash-repeat.json`. Hash/equality bodies in timing are deliberately cheap; expensive structural hashing will reduce the percentage benefit. Allocation-site removal is visible in the overlay but no heap bytes/GC reduction was measured. No application, browser, long-chain stress, cold-start or multi-engine result established.

## Correctness and F# semantic boundary

`hash-correctness.json` records 66 groups:

* Four 700-operation differential scenarios, Dictionary/HashSet with constant hashes and 11 buckets, compare every operation result, count, enumerated contents/order, and full hash/equality invocation trace. Includes updates, removals, hits, misses, clears, and active enumerator mutation in same/different buckets.
* Sixty comparator-effect scenarios: hash clears/throws; equality clears/adds/deletes/throws; each exercised through has, delete, get, set and Add entry points (Set get maps to has). Baseline/candidate outcomes, exceptions, final contents and callback traces match. Dictionary duplicate Add exceptions remain observable. Reentrant mutation is tested for baseline compatibility, not asserted to be guaranteed by .NET.
* Two mechanism gates instrument internal native maps. For one hit plus one miss, baseline performs 2 `has` + 1 `get`; candidate performs 0 `has` + 2 `get`. This confirms the specific intended mechanism, not a measured byte allocation saving.

One `GetHashCode` remains before bucket lookup, and equality visits exactly the same bucket in the same order with the same findIndex snapshot-length behavior. No user hash/equality/comparison is elided or cached. Keeping later re-fetches is essential: equality can clear or replace the underlying collection; retaining a stale bucket in setters/removers would change existing behavior. Enumeration code, bucket insertion order, collision resolution, public signatures and representations are unchanged. Persistent sharing does not apply to these mutable types; ordered persistent Map/Set were covered separately.

These are direct runtime differential tests using custom comparers and object keys, not a newly compiled F#/.NET fixture. They establish equivalence to the tested JS baseline, including any pre-existing .NET discrepancies, and do not supersede the parent hashing fixture's reported nested-option issue. No compiler/runtime conformance claim is made.

## Scope and blast radius

`src/Fable.Transforms/Replacements.fs:799-814` already selects native Map/Set for keys compatible with native comparison. Explicit custom comparers and non-compatible structural keys select MutableMap/MutableSet. This optimization affects that latter path and its lookup/update/remove operations, including transitively called collection methods. It cannot speed default primitive-key Dictionary/HashSet operations already lowered to native collections.

Estimated implementation: two private helper bodies plus, if needed, one narrowly named Native helper. Do not generalize to all Dictionary.TryGetValue replacements. Python's separate MutableMap.fs/MutableSet.fs has analogous bucket lookup but no JS-native invariant to reuse; no Python API/semantics change proposed. TypeScript source output and all JS collection suites still require verification against a coherent freshly generated runtime before shipping.

## Reproduction

Run sequentially from main root; no dependencies installed:

```powershell
rtk proxy python .worktrees/fickle/notes/benchmarks/runtime-hashing/keyed-collections/prepare.py hash
rtk node .worktrees/fickle/notes/benchmarks/runtime-hashing/keyed-collections/hash.mjs --check-only
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/keyed-collections/hash.mjs
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/keyed-collections/hash.mjs hash-repeat.json
```

The existing `temp/fable-library-js` is required. Preparation records input hashes and asserts expected helper shape; regenerated runtimes may differ. Artifacts and ignored disposable copies stay within this experiment directory. No commits, production changes, dependency installs or publishing.

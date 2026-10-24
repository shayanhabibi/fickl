# Array hashing: successful isolated experiment

## Suggested change

Apply the approach in [array.patch](array.patch): keep the captured array length and left-to-right element hashing, but fold each numeric hash directly into the accumulator instead of allocating an intermediate `number[]` and traversing it again. Public signatures and hash outputs remain unchanged.

## Experiments and results

Strict TypeScript compilation passed. The candidate passed 5,095 differential/behavior assertions; the combined candidate also passed 5,095. Allocation instrumentation confirmed the temporary hash-array constructor is eliminated (the unchanged implementation failed that gate). This does not quantify actual allocated bytes after JIT optimization.

Primary paired timing results from [paired-array.json](paired-array.json), 11 blocks per case:

| Input | Baseline ns/op | Candidate ns/op | Median paired time change | Winning blocks |
|---|---:|---:|---:|---:|
| Empty array | 5.77 | 5.42 | -6.1% | 10/11 |
| 1 numeric element | 8.57 | 6.21 | -26.6% | 11/11 |
| 4 numeric elements | 11.10 | 7.42 | -33.1% | 11/11 |
| 32 numeric elements | 42.88 | 23.12 | -45.9% | 11/11 |
| 1,024 numeric elements | 1,492.96 | 604.61 | -58.8% | 11/11 |
| Mixed values | 501.60 | 449.68 | -9.7% | 11/11 |
| Float64 typed array | 282.86 | 235.78 | -16.7% | 11/11 |
| Custom hash elements | 147.88 | 138.25 | -6.4% | 10/11 |

Unchanged numeric control: +0.04% paired time. Earlier fresh-process rounds also favored the array change. These are warm microbenchmarks on Node v26.7.0, not application-level speedups.

## Maintaining F# semantics

The fold uses the same zero seed and `((h << 5) + h) ^ elementHash`, retaining JavaScript's existing bitwise conversion/overflow behavior. Capturing length before traversal retains behavior when element getters/custom hashes resize the array. Reads and custom hash invocations remain in the same order, including sparse-array reads and exceptions. Only side-effect-free arithmetic on numeric hashes moves earlier.

Coverage includes sparse arrays, numeric typed arrays including bigint, long arrays beyond .NET hashing sampling thresholds, nested composites, NaN/infinities/-0, mutation and throwing hash methods. Exact hashes match the JS baseline. Actual F# Dictionary/HashSet and collision coverage had the same 1,885 passes and two pre-existing nested-option failures as baseline; see [shared methodology and caveats](README.md). No new observed semantic differences; full conformance is not claimed.

## Estimated blast radius

Implementation surface: one internal function in `Util.ts`. Behavioral reach: broad within JS/TS structural hashing—arrays, typed arrays, tuples represented as arrays, collection keys containing arrays, and nested composite hashes. Equality, ordering, representation and .NET compiler paths are unchanged. Any hash regression could break collection lookup, so downstream risk is medium despite the small patch. Custom hashes must honor the numeric return contract; malformed JS coercible returns are not covered.

Recommendation: strongest candidate to implement first, followed by full JS/TS regression suites and representative application measurements. Browser/other-Node performance and heap/GC benefits remain unmeasured.

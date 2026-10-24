# Record hashing: promising, workload-dependent experiment

## Suggested change

[record.patch](record.patch) retains `Object.values(self)` as a snapshot and folds its values directly into the numeric hash accumulator, removing only the `.map` result array. Keep the snapshot: reading fields during hashing can observe mutations that the existing implementation cannot.

## Experiments and results

Strict TypeScript compilation and 5,095 candidate differential/behavior assertions passed. Allocation instrumentation verified that hashing no longer calls `.map` to create the temporary hashes array; the values array still exists. Combined-candidate checks also passed.

Paired time changes, 11 blocks per run, from [first run](paired-record.json) and [isolated repeat](paired-record-repeat.json):

| Workload | First run | Repeat | Repeat winning blocks |
|---|---:|---:|---:|
| 4 fields | -12.8% | -14.5% | 11/11 |
| 16 fields | -8.1% | -8.1% | 9/11 |
| 64 fields | -2.5% | +0.8% | 4/11 |
| Nested composite | -19.0% | -14.8% | 11/11 |
| Hash-bucket lookup fixture | -3.7% | -3.7% | 11/11 |

Repeat unchanged controls: numeric -0.07%, array +0.47%. Small/medium record and nested workloads show repeatable gains; **large-record speedup is not established**. Earlier fresh-process runs were mixed, including a small-record regression, and are retained in [results.json](results.json). Paired results justify further development, not a universal performance claim. Hash-bucket lookup is synthetic, not the Fable Dictionary implementation.

## Maintaining F# semantics

The snapshot preserves enumerable own string-key value order and evaluates getters before any custom hash runs. Symbol/non-enumerable treatment remains that of `Object.values`. The number/order of `structuralHash` calls and numerical combine operation remain identical. Mutating hashes, getter side effects and exceptions were checked against baseline. Record nominal equality, comparison, prototypes and fields are untouched.

The actual F# collection fixture has identical outcomes for this candidate and baseline: 1,885 passing assertions and two existing nested-option failures, also reproduced using the original generated runtime. See [methodology](README.md) for this limitation and the numeric `GetHashCode` contract. No expected test result was weakened. These experiments show baseline equivalence under tested cases, not proof of complete F# semantic conformance.

## Estimated blast radius

Implementation surface: the shared `recordGetHashCode` helper in `Types.ts`. Reach includes ordinary records, nested record keys, and **FSharpException**, which uses the same helper. Anonymous records represented as plain objects use the separate object path. No layout/prototype/API change is needed. Risk is medium because this shared primitive affects hash-based collections; performance benefit is workload-dependent. Exception hashing deserves explicit suite coverage before shipping.

Recommendation: retain as a successful small/medium-record optimization candidate, implement separately from the array patch, and require full JS/TS suites plus coherent-runtime integration tests before release. Do not advertise a large-record or application-wide speedup.

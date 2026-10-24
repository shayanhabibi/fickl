# Plain-object hashing: successful paired experiment

## Suggested change

[object.patch](object.patch) changes the existing plain-object branch of `structuralHash`: preserve the `Object.values(x)` snapshot, then combine numeric hashes in a loop rather than mapping to a second array. Preserve branch selection and all other object handling.

## Experiments and results

Strict TypeScript compilation and 5,095 candidate differential/behavior assertions passed. The combined candidate also passed its 5,095 checks. Allocation instrumentation confirms removal of the `.map` hash array, not removal of the snapshot or a measured byte reduction.

Paired time changes, 11 blocks per run, from [first run](paired-object.json) and [repeat](paired-object-repeat.json):

| Workload | First run | Repeat | Repeat winning blocks |
|---|---:|---:|---:|
| 4 fields | -15.9% | -17.0% | 10/11 |
| 16 fields | -20.9% | -22.1% | 11/11 |
| 64 fields | -19.1% | -19.1% | 11/11 |
| Nested composite | -19.1% | -10.6% | 9/11 |
| Hash-bucket lookup fixture | -7.6% | -3.8% | 7/11 |

Repeat unchanged controls: numeric -1.46%, array -1.80%. Core object gains are substantially larger than controls and repeat across field counts. Lookup evidence is weaker and should not be extrapolated to applications. Initial fresh-process results included regressions for small objects; controls also moved materially. Those contradictory measurements remain in [results.json](results.json). The paired design reduces process-to-process JIT/noise differences but does not establish performance across engines or real applications.

## Maintaining F# semantics

`Object.values` still determines field selection/order and takes the complete values snapshot before user hash calls begin. Field replacement/deletion/addition caused by a custom hash therefore cannot alter which original values are hashed. Getter evaluation, symbol/non-enumerable exclusion, hash call order/count, exceptions and numeric combine operations match the baseline tests. Equality and object representation are unchanged.

Tests cover randomized nested plain objects and side effects; the actual F# fixture includes anonymous-record equality/hash/set use. All variants match the baseline fixture outcomes: 1,885 passes and two pre-existing nested-option failures. The original generated runtime reproduces those failures too. [Shared methodology](README.md) documents why this is regression evidence rather than full F# conformance, and the numeric hash-return contract required for fusion.

## Estimated blast radius

Implementation surface: one branch in `Util.ts`. Reach: plain objects entering structural hashing, anonymous records using that representation, nested object values and structural collection keys. Other class/custom-hash branches remain intact. No API, representation or compiler change is proposed. Risk is medium because incorrect hashes could break collection lookup despite the small diff. Arbitrary JS objects with nonnumeric custom hash returns or monkey-patched builtins are outside the validated boundary.

Recommendation: retain as a successful candidate, review independently and run full JS/TS suites with a coherent runtime before shipping. Validate supported browser/Node engines and application workloads; retained heap, GC and cold-start effects remain unmeasured.

# Standard-library experiment checklist

This is a bounded first pass across the observed candidates, not a claim that the standard-library frontier can be exhausted. Production files remain unchanged. Each experiment must save its report before its worker begins another experiment; unsuccessful and unsafe candidates get reports too. Independent investigation/correctness work runs in parallel; heavy builds and timing measurements require an exclusive slot from the coordinator.

## Required checks for every candidate

- Identify the actual existing implementation and compiler call sites, avoiding proposals for fast paths already present.
- State the needed compiler guarantee, its source, and whether it is available today or only a proposed analysis. Keep a fallback where it is not established.
- Use the full FCS type/entity/field/attribute and call-site context when looking for specialization: closed generic types, known built-in operations, recursively reflexive equality, value types, non-escaping locals and known layouts. Distinguish information FCS exposes from the additional effect/escape/range proof the compiler would need to implement.
- Compare baseline and candidate values, exceptions, callback/comparer/hash call counts and order, and mutation observations. Preserve F# intentions as well as common-case output.
- For equality: test distinct-but-equal allocations, same-reference values, NaN, signed zero, null, nested options, custom equality/comparison, and nominal type distinctions. Shallow immutable fields do not prove deep immutability, purity, or reflexivity. Reference inequality does not imply structural inequality.
- For collections: test collisions, custom comparers, persistent aliases, enumeration order, mutation and boundary sizes. For sequences additionally test laziness, disposal, short-circuiting and repeated enumeration.
- For strings/numbers: test UTF-16 behavior, null/error cases, overflow and special values; do not substitute JavaScript semantics silently for F#/.NET behavior.
- Save runnable tests, raw measurements and a report containing suggested changes, experiments/results, semantics argument, estimated blast radius, limitations and disposition.
- Separate existing baseline bugs from regressions. Do not change an assertion that passes on .NET merely to accommodate JavaScript.
- Use paired/warmed measurements with observable outputs and controls; do not time concurrent benchmarks/builds. No application-wide or cross-engine claim from one Node microbenchmark.

## First-pass queue

| Area | Status | Owner |
|---|---|---|
| Array/record/plain-object hash intermediates | Completed; see existing reports | Prior work |
| Nested-option baseline discrepancy | Completed diagnostic; compiler fix pending | [report](semantics-fastpaths/options-report.md) |
| Compiler-guaranteed equality/record fast paths | Completed scoped feasibility/timing; compiler implementation pending | [report](semantics-fastpaths/records-report.md) |
| Array/list intermediates and loops | Completed List candidate and rejected generic array fusion | [report](linear-collections/list-report.md) |
| Sequence iterator/closure overhead | Completed modest inner-loop candidate | [report](linear-collections/seq-report.md) |
| Ordered Map/Set traversal/allocation | Completed; unrestricted field-cache candidate rejected | [report](keyed-collections/ordered-report.md) |
| Dictionary/HashSet buckets/collisions | Completed promising private-bucket candidate | [report](keyed-collections/hash-report.md) |
| String helper intermediates | Completed promising UTF-16 loop candidate | [report](string-helpers/string-report.md) |
| Numeric helper specialization | Completed promising private-ref elimination | [report](numeric-helpers/numeric-report.md) |
| Struct representation / scalar replacement | Completed local numeric-struct candidate; copy/escape boundaries documented | [report](representation/report.md) |

The previous .NET FCS / TT[] frontier stays paused and partially explored. These investigations concern generated JS runtime behavior.

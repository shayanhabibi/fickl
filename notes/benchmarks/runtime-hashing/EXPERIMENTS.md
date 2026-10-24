# Standard-library and compiler-context experiments

These reports extend the original three hashing experiments. They use existing FCS information to identify possible fast paths, with additional required effect/alias/escape proofs made explicit. All production implementation and release decisions remain pending; the measured candidates are isolated overlays or modeled compiler output.

## Completed investigations

- [Nested options](semantics-fastpaths/options-report.md): an emitted option layer is lost around nullable string payloads. Restoring it in an isolated generated fixture passes all 1,887 original F# expectations. Compiler construction/extraction and representation compatibility need a coordinated fix.
- [Compiler-proven record equality](semantics-fastpaths/records-report.md): a positive identity shortcut is valid with recursively pure/reflexive equality. Existing runtime identity handling limits its benefit: modeled callsite specialization improves same-reference cases around 4–6% but regresses distinct/mixed cases around 3% in one run. A known-shape direct-field comparator improves distinct-record method workloads around 55–64%. Neither compiler transformation has been implemented.
- [List.append and Array.collect](linear-collections/list-report.md): one-pass unpublished list construction reduces intermediate list nodes and improves tested nonempty workloads around 57–82%. General streaming Array.collect is rejected because mapped arrays can alias and mutate. FCS-visible fresh mapper results could enable a narrower future specialization.
- [Seq.filter](linear-collections/seq-report.md): specializing the compiler-owned choose adapter preserves lazy/disposable machinery and usually improves tested workloads around 3–13%, with inconclusive cases documented. Eager array filtering is not a valid substitution merely because the source has array type.
- [Ordered Map/Set counts](keyed-collections/ordered-report.md): repeated-count caching has a large potential benefit, but the tested added-field cache changes freeze/reflection behavior and becomes stale after interop mutation. It is rejected as an unrestricted replacement. A proven local/non-escaping representation or another cache design needs a separate experiment.
- [Dictionary/HashSet bucket lookup](keyed-collections/hash-report.md): the compiler-selected structural/custom-comparer path can use its private nonnull-bucket invariant to remove a native has/get round trip and temporary byref machinery. Tested workloads improve around 18–47%; general native Map lookup cannot assume the same invariant.
- [String.filter](string-helpers/string-report.md): primitive strings permit direct UTF-16 traversal, removing explicit split/filter arrays. Tested workloads improve around 47–78%. Existing null handling differs from .NET and is documented separately.
- [Numeric parse](numeric-helpers/numeric-report.md): eliminate a provably private non-escaping ref cell in Double.parse. Valid-input workloads improve around 14–21%; exception-path results are inconclusive. Existing parsing compatibility is preserved rather than silently changed.

- [Local struct scalar replacement](representation/report.md): actual compiled numeric struct construction/copy-update can be replaced by locals for a proven non-escaping kernel, with 14–36% improvement in two runs. The five .NET kernel cases and 1,036 differentials pass. Existing mutable-struct copy aliasing in the tested prebuilt compiler is documented separately; boxing/interface/closure/interop materialization remains unchanged.

## Reading the evidence

Percentages describe elapsed time for the named Node microbenchmarks, not application-wide speedups, cross-engine guarantees or confidence intervals. Reports retain raw measurements and exact candidate mechanisms, semantic checks, required guarantees, estimated blast radius, rejected alternatives and prerequisites. Most runtime candidates still require compilation from the proposed F# source and coherent-runtime full JS/TS suites before rollout.

The runtime already optimizes some paths: native arrays and primitive-key dictionaries are examples. Compiler metadata must be used to inspect actual emitted call sites, rather than timing unused generic wrappers. Conversely, having FCS type information does not automatically provide a whole-program proof of purity, non-escape, non-NaN values or canonical allocation. A usable fast path states the conservative proof and fallback explicitly.

Final verification reran every candidate correctness harness successfully. Expected baseline conformance discrepancies remain recorded rather than treated as passing .NET behavior. Report links and JSON evidence were checked. Leftover MSBuild workers were stopped after all builds and measurements, excluding active Rider evaluations. Production source diffs remain empty in the compiler/runtime directories examined.

See [FRONTIER.md](FRONTIER.md) for the mandatory checks and queue, and [README.md](README.md) for original hashing methodology. The frontier remains partially explored.

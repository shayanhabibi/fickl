# Double.parse: remove the private ref-cell round trip

Completed 2026-10-23 after the string report. Successful isolated valid-input experiment; invalid-input timing is inconclusive. No production files changed.

## Suggested change and compiler context

`src/fable-library-ts/Double.ts:18` allocates `new FSharpRef(0)`, calls `tryParse`, then reads `contents`. That particular cell is private, cannot escape and is not user-provided. `Types.ts` implements it with getter/setter closures, so the source-level operation includes a wrapper and closures. Actual surviving heap allocations depend on the JS JIT.

[candidate.ts](candidate.ts) performs the exact existing validation/conversion directly and returns the numeric result; it retains the same failure exception and message. This removes the wrapper round trip. Production code could factor validation into a private numeric-result helper (using NaN as the failure sentinel already rejected today) to avoid duplicating parsing logic, but that refactoring was **not** the measured candidate and must be separately checked for inlining/code-size effects.

FCS already knows the numeric target type and the compiler's `parseNum`/numeric conversions route string-to-float operations to Double.parse (see `Replacements.fs:271`, `:2764`, `:2805`). This optimization needs no new public API or AST changes. More generally, a compiler can scalar-replace a ref cell proven private and non-escaping; that does **not** justify removing arbitrary caller-owned byrefs/out parameters. Here the helper itself supplies the proof by construction, including direct JS callers. Existing parse/tryParse interfaces and user-provided ref cells remain intact.

## Experiments and results

[experiment.mjs](experiment.mjs) reads the actual entire Double.ts module, requires an exact single match for the old parse body, emits isolated baseline/candidate modules using Node type stripping, and imports the existing generated FSharpRef and Exception dependencies. Source SHA-256 and Node/V8 versions are saved in [correctness.json](correctness.json). This measures the real parser bodies, not a replacement numeric algorithm.

**12,068 differential checks pass:** fixed edge cases and 4,000 randomized strings, parse result or exact error class/message, unchanged tryParse return and cell contents, plus interop coercion/replace traces and throwing replacement methods. Object.is-sensitive comparison includes negative zero, infinities and NaN behavior. Tests include blank strings, whitespace, exponents, overflow to infinity, malformed input, hexadecimal/binary and underscores according to current baseline behavior.

Two exclusive fresh-process paired runs: 11 alternating ABBA/BAAB blocks per case, two samples per variant per block, about 25 ms per sample following warmup, explicit GC between blocks. Checksums consume parsed numbers and error messages. Node v26.7.0 / V8 14.6.202.34-node.28, Windows.

| Workload | First paired time change | Repeat | Repeat winning blocks |
|---|---:|---:|---:|
| Unchanged tryParse control | -0.7% | -0.7% | 7/11 |
| Integer strings | -21.0% | -19.5% | 11/11 |
| Decimal strings | -17.3% | -15.9% | 11/11 |
| Exponent strings | -21.0% | -16.7% | 11/11 |
| Leading/trailing whitespace | -17.5% | -14.5% | 11/11 |
| Mixed currently valid strings | -17.3% | -15.4% | 11/11 |
| Invalid strings / exceptions | -6.4% | +1.5% | 5/11 |

Negative means less time. Valid cases improve in both runs substantially beyond the unchanged control; no repeatable invalid-input gain is established. Raw blocks: [timings.json](timings.json), [timings-repeat.json](timings-repeat.json). No confidence intervals, application measurements, browser testing or heap/GC byte measurements were performed.

## Maintaining F# semantics

The candidate retains the same nonnull/whitespace test, single-underscore replacement, unary numeric conversion, Number.isNaN rejection, and Exception message interpolation. It does not substitute parseFloat (which accepts numeric prefixes of invalid strings) or change overflow/format rules. Coercion and custom replace invocation order remain unchanged. The eliminated FSharpRef is created privately with a numeric value; its closure getter/setter have no user callbacks. No mutation of externally reachable storage is removed.

This is baseline-regression evidence, **not full .NET numeric parsing conformance**. The existing parser deliberately has a TODO about format/range validity and accepts inputs such as `0xff` or `1_000`; preserving those results is not a claim that System.Double.Parse accepts them. Any compatibility corrections need distinct unchanged .NET expectations and a separate behavior change. Culture-sensitive overload behavior was not expanded or validated here. Invalid interop inputs retain tested error traces, but globally monkey-patched runtime internals are outside the contract.

## Estimated blast radius

One parse helper, affecting generated string-to-floating conversions and applicable Parse calls (including single/double paths using this shared module), plus direct runtime consumers. tryParse and callers' byref cells are unchanged. No public signature or object representation change. Low-to-medium regression risk because parsing is common and input/error behavior is observable; duplicating validation creates a future maintenance risk. Full JS/TS numeric suites and coherent-runtime compiler tests remain necessary before rollout.

## Reproduction

From the Fickl worktree, with main-workspace generated `temp/fable-library-js` present:

```powershell
rtk node notes/benchmarks/runtime-hashing/numeric-helpers/experiment.mjs check
rtk node --expose-gc notes/benchmarks/runtime-hashing/numeric-helpers/experiment.mjs bench
rtk node --expose-gc notes/benchmarks/runtime-hashing/numeric-helpers/experiment.mjs bench timings-repeat.json
```

Run timings sequentially. Extracted temporary modules live under ignored `temp/runtime-numeric-helpers`; no dependency installation is required.

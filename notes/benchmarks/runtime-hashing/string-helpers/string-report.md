# String.filter: direct UTF-16 traversal

Completed 2026-10-23 before beginning the numeric helper experiment. Successful isolated performance experiment; production integration and null-semantics correction remain separate work.

## Suggested change

Replace `x.split("").filter(c => pred(c)).join("")` in `src/fable-library-ts/String.ts:743` with a loop over primitive string UTF-16 code units, appending selected units to a result string. [candidate.ts](candidate.ts) retains the original expression as a fallback for null/non-primitive JS interop inputs. This removes two explicit intermediate arrays for ordinary F# strings. It does not quantify heap bytes after JIT optimization.

The compiler replacement `stringModule` in `Replacements.fs` routes String.filter to this helper. Its string type guarantees primitive UTF-16 semantics for ordinary nonnull F# values; no new compiler analysis is necessary. **Do not use `for...of`**, which iterates Unicode code points and would change which F# `char` values the predicate receives.

## Experiments and results

[experiment.mjs](experiment.mjs) extracts the actual baseline function from source, asserts its expected signature, and strips its TypeScript annotations using Node's built-in type stripping. Only the extracted helper is measured; this avoids unrelated dependency overhead and does not substitute a hand-written baseline. Candidate TypeScript passed local TypeScript 7.0.2 strict `--noEmit` checking. Source hash, Node/V8 versions and results are saved in [correctness.json](correctness.json) and the timing files.

**6,044 differential checks pass:** 1,208 fixed/random strings under five predicate behaviors (all/none/parity/stateful/throwing), plus null/undefined/String-object fallback and an explicit surrogate-half check. Coverage includes empty strings, embedded NUL, combining characters, CJK, astral pairs and unpaired surrogates; callback argument/order/count and thrown error class/message match baseline. The emoji predicate receives `[55357, 56832]`. An independent .NET FSI experiment records those same code units in [records-dotnet-results.txt](../semantics-fastpaths/records-dotnet-results.txt); see the [record report](../semantics-fastpaths/records-report.md) for capture details.

Two exclusive paired timing runs on Windows / Node v26.7.0 / V8 14.6.202.34-node.28, lengths 0, 8, 64, 1,024 and 16,384, with all/half/no characters selected. Nine alternating ABBA/BAAB blocks per case, two samples per variant per block, approximately 20 ms per sample after warmup, explicit GC before each block. Every batch consumes output length and a middle code unit to avoid timing only deferred string construction. Checksums match.

| Length | All selected: first / repeat | Half selected: first / repeat | None selected: first / repeat |
|---|---:|---:|---:|
| 0 | -69.0% / -69.8% | -69.5% / -70.5% | -70.2% / -71.4% |
| 8 | -77.2% / -78.2% | -70.1% / -69.9% | -68.7% / -70.2% |
| 64 | -57.5% / -56.4% | -46.9% / -47.4% | -58.1% / -64.7% |
| 1,024 | -55.5% / -68.3% | -54.2% / -58.0% | -62.8% / -60.2% |
| 16,384 | -55.5% / -55.6% | -59.4% / -54.0% | -54.7% / -58.5% |

Negative means less elapsed time. Candidate wins all nine blocks in every case in both runs. Raw samples: [timings.json](timings.json), [timings-repeat.json](timings-repeat.json). Results are descriptive, without confidence intervals or an unrelated unchanged control. Paired checks and repeated large gains support this helper-level result; they establish neither browser performance nor application throughput, retained heap, or behavior at enormous string sizes. Expensive predicates can dominate and reduce the proportional benefit.

## F# semantics and existing discrepancy

For nonnull primitive strings, input contents are immutable and primitive indexed reads have no getters. Moving each read from the initial split to immediately before its predicate call therefore preserves values and observable callback effects. The predicate still runs once per UTF-16 unit, left to right, stops on the same exception and treats its boolean result the same way. No user-provided conversion runs during concatenation of primitive one-unit strings.

The .NET diagnostic also found **String.filter on null returns the empty string**, while this JS baseline throws TypeError. The isolated candidate intentionally reproduces the baseline fallback, including error message; it does not fix this existing discrepancy silently during a performance change. Thus 6,044 passing differential checks do not imply full .NET conformance. A production proposal should add an unchanged F# null expectation and fix that behavior in a distinct correctness change, checking null predicate and zero-length behavior as well. The timing evidence covers nonnull primitive strings only.

## Estimated blast radius

One exported runtime helper; generated F# String.filter and direct JS consumers calling it. No signature, representation, option, equality or other target changes are needed. Risk is low-to-medium: UTF-16 iteration and predicate exception/call behavior are the main hazards, with explicit tests. The fallback preserves nonprimitive/null baseline behavior. String rope/flattening costs can vary across engines and consuming workloads. Full JS/TS suite and supported browser/Node tests remain required before shipping.

## Reproduction

From the Fickl worktree:

```powershell
rtk node notes/benchmarks/runtime-hashing/string-helpers/experiment.mjs check
rtk node --expose-gc notes/benchmarks/runtime-hashing/string-helpers/experiment.mjs bench
rtk node --expose-gc notes/benchmarks/runtime-hashing/string-helpers/experiment.mjs bench timings-repeat.json
```

The harness defaults to main-workspace source; `FABLE_STRING_SOURCE` can supply another absolute String.ts path. Temporary extracted modules go to ignored `temp/runtime-string-helpers`. No production files changed.

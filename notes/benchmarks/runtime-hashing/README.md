# JavaScript runtime hashing experiments

The later standard-library and full-FCS-context investigations are indexed in [EXPERIMENTS.md](EXPERIMENTS.md), with mandatory checks and scope in [FRONTIER.md](FRONTIER.md).

Date: 2026-10-23. These are isolated experiments, not production changes or a release.

| Candidate | Conclusion | Report |
| --- | --- | --- |
| Array hash accumulator | Strong microbenchmark result; prioritize | [Array report](array-report.md) |
| Record hash accumulator, retaining values snapshot | Promising for small/medium records; large records inconclusive | [Record report](record-report.md) |
| Plain-object hash accumulator, retaining values snapshot | Promising and repeatable in paired tests | [Object report](object-report.md) |

## Scope and environment

Source: main workspace `src/fable-library-ts/Util.ts` and `Types.ts`, commit `26e40937dd0ccdfbbe2f8281fb3bcb4aea8848b9`; exact normalized source hashes are in [manifest.json](manifest.json). Artifacts live in the Fickl worktree, but source is the newer main workspace runtime. Preparation fails if expected source fragments have changed. Local TypeScript compiled isolated copies with strict checking. Windows; Node v26.7.0. Integration compiler: Fickl 0.5.3 / Fable 5.23.0, .NET target net10.0.

These optimize generated JavaScript applications. They do not establish any improvement to .NET FCS or Fickl compiler performance. The previously paused TT[] frontier remains partial.

## Experiments and interpretation

1. Built baseline, array-only, record-only, object-only, and combined variants in `temp/runtime-hashing`. Suggested source diffs are saved alongside the reports.
2. Ran allocation gates against unchanged copies first: the array gate failed as intended because the temporary hash array remained. All candidate gates passed. The gates detect constructor/map allocation mechanisms, not heap bytes or GC savings.
3. Ran 20,380 differential/behavior assertions across four candidates. All passed. Tests cover 5,000 randomized nested values per candidate, empty/long/sparse arrays, typed arrays, primitive edge cases, property order, getter and custom-hash side effects, mutation, throws, equality and ordering. See [correctness.mjs](correctness.mjs).
4. Ran seven rotated fresh-process benchmark rounds: [results.json](results.json), [measure.py](measure.py), [benchmark.mjs](benchmark.mjs). Array results were strong, but record/object results conflicted with later measurements and unchanged controls moved appreciably. These results are retained, not treated as decisive evidence for record/object speedups.
5. Ran paired measurements within each process: baseline/candidate ABBA or BAAB blocks, 11 blocks per case, two samples per variant per block, about 25 ms per sample following warmup, explicit GC before each block. Checksums must agree. Reports show median paired percentage changes, not ratios of pooled medians. These are descriptive measurements, not statistical confidence intervals. [paired.mjs](paired.mjs) and `paired-*.json` retain every block. Record/object reports include a separate repeat. An additional array repeat is retained as exploratory only: its final portion may overlap the initial record repeat; that record repeat was replaced by an isolated rerun. The primary array measurement and reported record/object runs were not deliberately run alongside builds or other benchmarks. Ordinary workstation noise remains possible.
6. Ran the actual F# collection fixture on .NET and generated JS. .NET: **1,887/1,887 pass**. Original generated runtime, rebuilt baseline, all individual candidates, and combined: **1,885/1,887 pass**, identical failing assertions 1814 and 1822. See [Fixture.fs](Fixture.fs), [fixture-results.json](fixture-results.json), and [original-runtime-fixture.json](original-runtime-fixture.json).

The fixture exercises real Fable Dictionary/HashSet operations, array and record keys, updates/removes, anonymous records, deliberately colliding custom hashes, and nested options. The two failures are insertion of `Some (Some null)` as a distinct nested-option set member, and the subsequent expected set count of five. They reproduce without the experimental Util/Types overlay. No expected assertion was weakened: checks accumulate failures so the collision tests still execute, then the fixture exits unsuccessfully. This is an existing semantics discrepancy in the tested compiler/runtime combination; attributing it precisely to compiler versus runtime needs separate investigation. `check-fixture.py` verifies candidate equivalence to baseline, **not full F# conformance**, and records the nonzero fixture exit codes.

## Semantic boundary

All candidates preserve the numerical combine operation, zero seed, order and number of structural hash calls. Record/object candidates keep `Object.values` because it collects values before hashing starts. Replacing it with live property reads can change behavior when getters or custom hash implementations mutate fields.

Hashing assumes the F# contract that `GetHashCode` returns a number/int. Fusion interleaves combining with hash calls, whereas baseline combines after obtaining every hash. Combining primitive numbers has no user-visible effects. Malformed JS interop hash methods returning coercible objects can observe changed coercion timing; arbitrary monkey-patched Array builtins also fall outside the asserted compatibility boundary. No option representation, equality, comparison or public API changes are proposed. Hash numbers are compared to the existing JS baseline, not required to equal .NET hash numbers.

## Reproduction

From the Fickl worktree, with main-workspace dependencies and generated `temp/fable-library-js` present:

```powershell
rtk proxy python notes/benchmarks/runtime-hashing/prepare.py
rtk node notes/benchmarks/runtime-hashing/correctness.mjs temp/runtime-hashing --allocation-gate
rtk node --expose-gc notes/benchmarks/runtime-hashing/paired.mjs temp/runtime-hashing array notes/benchmarks/runtime-hashing/paired-array.json
rtk node --expose-gc notes/benchmarks/runtime-hashing/paired.mjs temp/runtime-hashing record notes/benchmarks/runtime-hashing/paired-record.json
rtk node --expose-gc notes/benchmarks/runtime-hashing/paired.mjs temp/runtime-hashing object notes/benchmarks/runtime-hashing/paired-object.json
rtk dotnet run --project notes/benchmarks/runtime-hashing/Fixture.fsproj -c Release
rtk proxy python notes/benchmarks/runtime-hashing/prepare-fixture.py
rtk proxy python notes/benchmarks/runtime-hashing/check-fixture.py
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File notes/benchmarks/cleanup-msbuild.ps1
```

Wait for each benchmark to finish before starting the next. `prepare-fixture.py` requires the existing verified Fickl 0.5.3 executable under `temp/release-0.5.3`; the generated full runtime is an environment prerequisite, not pinned by the two-source manifest. Treat integration reproduction across regenerated runtimes accordingly. The fixture baseline is restored after variant checks.

## Recommended next action and limits

Prioritize the array patch, then review record/object patches independently. Before shipping, run the full JS/TS suites against a coherent freshly built runtime and measure representative applications and supported Node/browser engines. No browser-engine, end-to-end application, cold-start, retained-heap or GC-throughput benefit has been established. The benchmark's `hash-lookup` is a small native-Map bucket workload using runtime hashes/equality, not an actual Fable Dictionary timing measurement. The F# fixture supplies actual collection correctness coverage instead.

No production sources were edited and no package was published. Leftover MSBuild workers were stopped after the runs, excluding active Rider evaluations.

## Verifying integrated source changes

`verify-integrated.py` compares complete generated runtimes rather than constructing candidate overlays.
Build and save an unoptimized runtime first (the resynced baseline is commit `0dffdfcde`), then rebuild
after applying the source changes. Run each step after the preceding build has finished:

```powershell
rtk proxy python notes/benchmarks/runtime-hashing/verify-integrated.py save --runtime PATH_TO_BASELINE_RUNTIME
rtk dotnet run --project src/Fickl.Build -- fable-library javascript
rtk proxy python notes/benchmarks/runtime-hashing/verify-integrated.py check
```

Snapshots, source hashes, and detailed results stay under ignored `temp/runtime-resync/`.
Existing snapshot names are protected; use `check --name candidate2` for another run.
The checks cover hashes and callback traces, list sharing and constructor counts, sequence disposal,
private bucket lookups and comparator effects, UTF-16 filtering, and numeric parsing. Historical hash
variant labels all load the actual combined candidate. The allocation gates intentionally fail against
an unoptimized candidate. This script performs correctness checks only; timings remain separate.

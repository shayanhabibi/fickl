# Watch latency probe

`measure.py` (version 2) drives a real `fable --watch` process against a task-owned F# fixture
generated under an explicit scratch directory, then checks every edit against a fresh compilation.
It accepts an explicit compiler executable or DLL and an existing generated JavaScript runtime
library; it never builds or publishes a compiler. Prerequisites: Python 3.11+ (high-resolution
`time.sleep` on Windows), .NET SDK 10, Node.js, RTK, restored SDK packages. Use a fresh scratch
directory per run. The version 1 driver is preserved in
`temp/ralph-compilation/watch-floor/harness-original/`; the old `fixture/` folder is unused by v2.

```powershell
rtk python notes/benchmarks/watch-latency/measure.py --compiler <fable.dll> --runtime-lib <fable-library-js> --scratch <fresh> --prepare-only
rtk python notes/benchmarks/watch-latency/measure.py --compiler <fable.dll> --runtime-lib <fable-library-js> --scratch <fresh> --delays 200 50 10 --rounds 2 --cases <subset>
rtk python notes/benchmarks/watch-latency/summarize.py <scratch>/results.json
```

## Fixture

`render(state)` generates the whole project: `Lib.fsi`/`Lib.fs` (signature pair), `Shared.fs` with
12 `Broad*.fs` dependents, `Input.fs` with `Narrow.fs` and `App.fs` dependents, an optional
`Extra.fs`, an optional `Ref/Ref.fsproj` project reference, and an `App.fs` exporting every value
plus `#if DEBUG` and `#if PROBE_DEFINE` constants. `expected(state)` gives the exact exports;
`verify.mjs` imports the generated `App.js` and requires each one.

## Protocol

- Watch and fresh commands both pass `--configuration` (default `Release`, the CLI otherwise uses
  Debug for watch and Release for a single compile). The conditional-define export makes a mismatch
  fail equivalence; `--fresh-configuration Debug` is the negative control for that.
- Edit classes (`EDITS`): body edits with unchanged signature, a three-save burst with distinct
  values 31/32/33 (the verifier must reject 31, a built-in red check), atomic replace, a no-op
  identical rewrite, narrow and broad signature changes, a local error and repair (diagnostic must
  name `missingWatchProbeName`), a rename breaking dependents and its repair, `.fsi` body and
  signature edits, and project changes (DefineConstants, added file, added project reference,
  then an edit inside the reference). An options change affects later edits, so run project cases
  in their own sessions (`--cases proj-define body-final`, and so on).
- Every `.js` file of the watch output is sampled every ~1.5 ms (stat, then digest on change).
  Raw transitions, including transient partial/empty writes, are kept; output readiness is the
  first observation of each changed file's final settled digest. `Event.wait` is not used for the
  sampling: on Windows it sleeps whole 15.6 ms timer ticks.
- Stdout is read by a dedicated thread; `edit_to_watching_ms` ends at receipt of the `Watching`
  line, after the console logger's background queue, the pipe and the RTK relay. Compiler-reported
  `Fable compilation finished in` and the `--timings` block are recorded per cycle.
- After each edit settles, the watch output and the edit's sources are snapshotted. After the
  watcher exits, each snapshot's sources are restored in place and compiled fresh with `--noCache`
  into a sibling folder; JS digests, diagnostics (newline-split: FCS uses `\x1d` inside
  suggestion lists) and semantics must match. Mismatches are recorded as
  `invalidation_failures` (the run exits 1) rather than aborting, so every class gets an outcome.
- `--session-env KEY=VALUE` applies to the watch process only (e.g. `FICKL_FCS_SIGNATURE_CUTOFF=0`).
- Timeouts and `Watch.close` kill the launched tree (RTK wrapper, compiler, any descendants)
  identified by pid *and* creation time through CIM, then verify each member exited. A failing
  `taskkill` for a still-live member raises. Unrelated processes are never touched.
- All fixture writes go through `inside()`, which resolves symlinks/junctions and requires the
  result inside scratch.

## Checks

`rtk python -m unittest test_driver` (run inside this folder): preparation launches nothing,
configuration pinning, readiness against transient writes (synthetic and real files), observer
stops when an edit throws, diagnostics matching, junction escape, an owned child-tree timeout
(rtk -> python -> python, all must exit) and a simulated failed kill. `rtk python
mutation_check.py` reverts each preflight fix in turn and requires the corresponding test to fail.

## Delivery checkpoint

The `perf/watch-latency` branch carries the three-file candidate-2 implementation and this
regression harness. The source checkpoint is `4c4f6137f`; release/package validation and merging
into the delivery branch remain separate. The default changes from200ms to50ms; saves in that
interval may now cause intermediate compilations. `--watchDelay` still overrides the default.

`throttle-race/` preserves the exact stress fixture and its15 candidate-2 observations. The
original executable's exit status did not reject duplicate/overlapping delivery, so validate its
JSON as well (missing observations and fields fail closed):

```powershell
rtk proxy python notes/benchmarks/watch-latency/throttle-race/test_results.py
rtk proxy python notes/benchmarks/watch-latency/throttle-race/check_results.py notes/benchmarks/watch-latency/throttle-race/results-cand2.jsonl
```

These JSON controls launch no compiler or background process. All15 saved observations have
zero loss, duplicates, spurious events and overlapping delivery. This does not establish disposal
or late-callback safety; those pre-existing limits remain outside the narrow buffer/producer fix.
The stress project can be rebuilt with `dotnet build -p:CliDir=<absolute-CLI-directory>/`; the
property selects the compiler assemblies under test.

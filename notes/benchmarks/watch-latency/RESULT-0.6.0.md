# Watch edit-to-done floor, Fickl 0.6.0 (eeaa02951)

Source checkpoint: the `perf/watch-latency` branch contains the candidate-2 implementation
in `src/Fable.Cli/Entry.fs`, `src/Fable.Cli/Main.fs` and `src/Fable.Compiler/Util.fs`.
This is committed implementation and regression evidence; it is not a published NuGet release.
The final release/combined-candidate audit remains pending. The throttle lock covers event
buffering and producer timer resets, not full observer serialization or disposal safety.

The full report, raw timelines and patches are in `temp/ralph-compilation/watch-floor/`
(`REPORT.md`, `runs/`, `candidate.patch`, `trace.patch`). This note keeps the durable numbers.
Windows 11, Ryzen 9 9900X; clock QPC.

## Baseline floor

A body edit at the 200 ms default takes about 216 ms from write to `Watching`:

| Segment | Time |
| --- | --- |
| FileSystemWatcher callback | 0.3 ms |
| Debounce plus `System.Timers.Timer` overshoot on the 15.6 ms tick | **208.9 ms** |
| Agent and cycle setup (no re-crack) | 0.6 ms |
| FCS incremental check | 2.5 ms |
| Fable transform, print and write | 1.2 ms |
| Logs and stdout | 0.6 ms |

- At delay 10 the debounce segment is 17 ms, because of tick quantization.
- Compiler-reported `Fable compilation finished in` covers only about 2–5 ms of this.
- On the JS suite (`Util.Testing.fs` body edit) edit-to-done is 238 ms, with compile at 25 ms.
- Project edits are dominated by MSBuild re-evaluation: 0.9–1.8 s.

## Candidate (isolated patch; not adopted)

The patch has three parts:

- default `--watchDelay` 200 → 50 ms;
- a synchronized `Observable.throttle`;
- a full recompile when project options change.

Paired, interleaved, each binary at its own default:

| Build | Rounds | Median paired diff | Pairs faster | Body edit |
| --- | --- | --- | --- | --- |
| IL | 5 | −148.4 ms | 80/80 | 211 → 67 ms |
| Composite R2R | 5 | −148.8 ms | 85/85 | 211 → 67 ms |
| JS suite | 5 | — | — | 238 → 85 ms |

- Every fixture session matched a fresh `--noCache` compile (JS digests, diagnostics, semantics).
- Saves 50–200 ms apart now compile the intermediate states. In the measured slow burst that meant
  3 cycles instead of 1, still finishing 131 ms sooner with the correct final value.

## Correctness findings (red → green with the candidate)

- **Options change.** A `DefineConstants`-only `.fsproj` edit created a new checker but compiled
  zero files, leaving stale output and broken later edits. The same code is in upstream Fable.
- **Throttle race.** The real `Observable.throttle` at a 1 ms delay lost 12 of 480k events in 3 of
  5 runs, and four concurrent producers crash `Timers.Timer`. It was not observed at ≥ 5 ms or end
  to end through FileSystemWatcher.

## Open

- Edits to MSBuild-imported files are not watched.
- A package-cache miss blocks the cycle (source-reviewed; about 3.5 s).
- The `DllRef`/`DllRef_1` output-directory naming race: 1 of 10 watch initial compiles.
- `JsInteropTests.js` discriminator member choice varies between fresh compiles.
- The compiler test suite depends on stale `TestProject` outputs; it passes 237/237 for both
  binaries with a neutral seed.
- Timer overshoot, first-edit JIT and the polling watcher were not reduced.

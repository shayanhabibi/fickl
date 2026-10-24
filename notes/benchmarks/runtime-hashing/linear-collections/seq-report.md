# Seq.filter callback specialization

2026-10-23. This experiment began after the List.append report was complete. Recommendation: modest candidate for follow-up compilation, behind the stronger List.append result. No production code changed.

## Candidate and source

`src/fable-library-ts/Seq.fs:518` implements filter as choose with a callback that wraps accepted elements in Some. The generated `choose` invokes that adapter for every input element; the adapter invokes the predicate. Specializing the compute loop removes the adapter callback and the nullable-current check on every rejected element. It keeps `generate`, `ofSeq`, `some`, and `disposeSafe`, so the existing enumerator state machine and option representation remain intact. `seq-candidate.js.txt` is the exact hand-written JS replacement tested inside an otherwise unchanged generated Seq.js module. No intermediate element collection exists in baseline filter, and this candidate does not claim to remove one.

Suggested F# implementation (not applied or compiled):

```fsharp
let filter f (xs: seq<'T>) =
    generate
        (fun () -> ofSeq xs)
        (fun e ->
            let mutable found = false
            let mutable current = Unchecked.defaultof<'T>
            while not found && e.MoveNext() do
                let x = e.Current
                if f x then
                    current <- x
                    found <- true
            if found then Some current else None)
        (fun e -> e.Dispose())
```

This sketch expresses the semantics; its generated JS could differ from the early-return JS candidate and must be measured independently before a production claim. A compiler-owned specialization or direct emitted implementation could match the measured loop. Dart `Seq.fs:460` also delegates filter to choose; Python `seq.fs:508` imports its native implementation. No shared compiler transform or other target was modified.

## Semantic checks

1,649 assertions passed in `seq-correctness.mjs`. Fifteen scenarios compare full baseline/candidate traces for enumeration acquisition, MoveNext, Current, predicates and disposal. They cover never-enumerated sequences, accept-all/reject-all/mixed predicates, re-enumeration, short-circuit exists, explicit early disposal, disposal before moving, repeated disposal, Current before/after enumeration, and exceptions from GetEnumerator, MoveNext, Current, predicate and Dispose. Exception object identity is preserved. Additional tests cover 800 integer length/predicate combinations, null/undefined and nested option identity, callback mutation of the backing array, lists, typed arrays, sparse arrays, JS generators, and null-source failure timing. Raw scenario traces are in `seq-correctness.json`.

Creation remains lazy: constructing filter invokes neither source GetEnumerator nor predicate. Calling the filtered GetEnumerator acquires the source exactly as before. Each MoveNext consumes only until the next accepted item; predicate count and order, single Current read per inspected item, disposal on exhaustion/error/short-circuit, and re-enumeration are preserved through the unchanged generate machinery. Null and nested-option elements still use Some and are not confused with end of sequence.

The compatibility claim is for the tested generated runtime and valid F# callbacks. No .NET or generated F# fixture was run here. Arbitrary JS monkey-patching and stack-inspecting callbacks are outside this experiment. Existing JS iterator bridges' behavior is preserved rather than redesigned; tests use Fable's disposable enumerator or Seq.exists when early disposal matters.

## Compiler specialization boundary

The full typed FCS context identifies the exact FSharp.Core SeqModule.Filter/Where call, source element type and predicate type. Current `Replacements.fs:2113–2140` routes Filter through the generic Seq runtime call; it does not already specialize choose's adapter or turn this call into native Array.filter, even when the source expression has an array type. The guarantee enabling this candidate is the known library implementation of the adapter: it invokes the predicate once and returns Some of the same element or None, with no independent effects. The predicate itself need not be pure. Specializing this compiler-owned adapter therefore requires no speculation about arbitrary JS functions or user callback effects. `Seq.where` aliases filter in the generated runtime and inherits the candidate when the function is replaced.

A fused terminal `Seq.map f |> Seq.exists p` loop can remove an entire enumerator layer, but is a separate candidate, not measured here. Specializing known compiler-owned operations must preserve argument evaluation order, source acquisition, exception/disposal ordering, repeated enumeration when a sequence escapes, and option semantics. Merely knowing an input is an array does not justify eager filtering or caching: mutation between construction and enumeration is observable. This experiment deliberately retains the sequence state machine and only specializes filter's inner loop.

## Measured results

Windows, Node v26.7.0, two sequential exclusive runs. Same paired harness as the list experiment: nine ABBA/BAAB blocks, 20 ms per sample, two samples per variant/block, 40 ms warmup per variant, GC before blocks. Each call constructs and fully consumes a filtered sequence (sum), except the separately named short-circuit case. Inputs are prebuilt. Negative percentages indicate less time; these descriptive medians are not confidence intervals.

| Input | Size / acceptance | Primary | Repeat |
|---|---|---:|---:|
| Array | 32 / all | -5.0% | -5.6% |
| Array | 32 / every tenth | -13.2% | -13.0% |
| List | 32 / all | -8.5% | -8.3% |
| List | 32 / every tenth | -4.6% | -12.4% |
| Generator | 32 / all | -4.0% | -5.8% |
| Generator | 32 / every tenth | -5.9% | -5.3% |
| Array | 1024 / all | -7.1% | -7.3% |
| Array | 1024 / every tenth | -10.7% | -11.1% |
| List | 1024 / all | -3.2% | -4.8% |
| List | 1024 / every tenth | -10.1% | -9.8% |
| Generator | 1024 / all | +0.3% | -3.4% |
| Generator | 1024 / every tenth | -5.3% | -3.3% |
| Null/undefined options | 1024 / all | -2.7% | -2.4% |
| Filter then exists | six inputs, stop at 4 | -3.3% | -16.4% |

Raw data: `seq-timing.json`, `seq-timing-repeat.json`, all blocks, sample counts and checksums retained. Most cases show modest improvements; generator1024/all is inconclusive, and short-circuit/List32-sparse estimates vary substantially. The candidate removes one lexical adapter allocation per filter construction and one call per input element at source level, but JIT inlining/allocation elimination may already absorb these; no allocation counter, bytes or GC saving is established for Seq.filter. The same accepted-value option representation and enumerator allocation remain.

## Reproduction and limits

After `prepare.py` has copied the existing generated runtime:

```powershell
rtk proxy python .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/prepare-seq.py
rtk node .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/seq-correctness.mjs
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/bench.mjs seq seq-timing.json
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/bench.mjs seq seq-timing-repeat.json
```

Run timings sequentially in an exclusive slot. `manifest.json` records all baseline JS dependency hashes; `seq-manifest.json` records Seq.fs and Seq.js. Inputs come from the main workspace, not the older worktree source. Copies under ignored `isolated/` are reproducible generated material. There is no browser, real application, retained heap, or full compiler-suite evidence. Blast radius is all JS/TS Seq.filter/where consumers; source surface is one function. Before rollout compile the source implementation, rerun semantic and JS/TS suites, and measure supported engines.

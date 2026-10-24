# List.append and rejected Array.collect fusion

Completed before beginning the sequence experiment, 2026-10-23. Recommendation: review a one-pass List.append builder. Do not stream Array.collect mappings without a proof of independence.

## Source and proposed change

Main-workspace `src/fable-library-ts/List.fs:391` implements append as `fold (...) ys (reverse xs)`. Each nonempty left node is copied into a reversed intermediate list, then copied again into the result. `List.collect` already uses a forward builder, establishing the runtime's private construction pattern. Python `fable_library/list.fs:382` and Dart `List.fs:362` use the same reverse/fold append; this report changes neither.

Suggested F# implementation (not applied):

```fsharp
let append (xs: 'T list) (ys: 'T list) =
    let root = List.Empty
    let mutable node = root
    let mutable rest = xs
    while not rest.IsEmpty do
        node <- node.AppendConsNoTail rest.Head
        rest <- rest.Tail
    node.SetConsTail ys
    root.Tail
```

`list-candidate.js.txt` is the exact hand-translated JavaScript experiment, patched into a full isolated copy of the actual generated runtime; baseline is unchanged generated List.js. This is not yet compiler-generated output of the proposed F# source. `manifest.json` hashes the input source and every copied generated JS dependency. No production code was modified.

### What the compiler already knows

Full typed FCS context resolves the FSharp.Core ListModule.Append operation and immutable list types; no dynamic JS duck-typing test is needed. `Replacements.fs:2427–2453` routes ListModule.Append through the generic List runtime call, while `Replacements.fs:1672–1674` routes `@` directly to List.append. The measured reverse/fold implementation is therefore still on these call paths; neither route already emits the forward builder. The useful static guarantee is immutable list spine plus fresh, unpublished construction nodes, not purity of the element type. Element values may be mutable and remain shared. This supports a runtime fix benefiting compiler-recognized call sites without adding a new AST construct or per-element dispatch.

For arrays, current replacement already lowers Array.filter to native `.filter` (`nativeArrayFunctions`, lines 2277–2287 and 2385–2388), so optimizing an unused generic Array.filter wrapper would not establish an application win. Array.collect is not in that table and reaches Array.collect through lines 2395–2400. Typed FCS knowledge is useful for restricted syntactic mapper specializations (e.g. a compiler-visible fresh array literal with nonescaping storage); it does not make every arbitrary user mapper return a fresh array. No such alias/effect proof was implemented or benchmarked here.

## Correctness and allocation

`list-correctness.mjs`: 48,337 successful assertions. Left lengths 0–150 crossed with right lengths 0, 1, 5, 50; output values, input nonmutation, element reference identity, exact right-tail identity, and empty-left identity checked. Instrumented actual FSharpList constructors confirm total node allocations of `2n+1` versus `n+1` for n=0,1,8,128,4096 (counts exclude input construction). This is allocation-count evidence, not measured bytes, retained heap, or GC time.

No user callback is involved. Elements are copied as references and never compared, hashed, coerced, or enumerated individually; mutation of an element remains observable through both lists. Input nodes remain untouched. A fresh result prefix ends in the original right list, including the empty-left case. Valid F# immutable lists are the boundary: JS proxies or accessors replacing node fields, cyclic/corrupt lists, monkey-patched runtime functions, and allocation failure can observe different traversal/allocation behavior and are not covered. No .NET fixture or full compiler/test-suite run was performed for this experiment.

## Timing

Windows, Node v26.7.0. Two exclusive runs; 9 alternating ABBA/BAAB blocks per case, 20 ms per sample, two samples per variant/block, 40 ms warmup per variant, explicit GC before blocks. Outputs escape to a global sink. Input generation excluded. Median paired candidate time change (negative is faster):

| Left length | Primary | Repeat |
|---|---:|---:|
| 0 | +4.7% | -4.2% |
| 1 | -57.2% | -60.5% |
| 8 | -76.0% | -75.9% |
| 32 | -80.4% | -81.7% |
| 128 | -81.5% | -79.6% |
| 1024 | -75.8% | -78.7% |
| 8192 | -77.7% | -80.4% |

Every sample, iteration count and checksum is retained in `list-timing.json` and `list-timing-repeat.json`. Absolute baseline times drifted between runs (e.g. n=128 about 2843 ns versus 5574 ns); do not present pooled ratios or precision estimates. Relative direction is strong for nonempty inputs; empty case is inconclusive. Microbenchmarks do not establish application or browser improvements. Runtime changes affect all JS/TS List.append calls and internal callers, with a small code surface but broad usage. Before rollout compile the F# change, run JS/TS suites and representative applications on supported engines.

## Rejected array candidate

`Array.fs:213` first maps every source element, then concatenates mapped arrays. Streaming each returned array immediately would avoid the outer intermediate array but changes F#-observable mutation semantics. Runnable counterexample in `list-correctness.mjs`: mapping sets `shared[0] = x` and returns the same shared array for inputs `[1,2]`. Actual Array.collect returns `[2,2]`; streaming candidate returns `[1,2]`, despite identical callback order/count `[1,2]`. The singleton path additionally returns the mapped array itself in current concat. Do not substitute flatMap or a streaming push loop generally. A compiler could only exploit fresh, nonescaping, nonmutating mapped arrays with adequate effect/alias proof; type information alone does not prove that. No timing was run for this rejected candidate.

## Reproduction

From main workspace:

```powershell
rtk proxy python .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/prepare.py
rtk node .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/list-correctness.mjs
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/bench.mjs list list-timing.json
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/linear-collections/bench.mjs list list-timing-repeat.json
```

Run timing commands sequentially with no builds or other benchmarks. `isolated/` is ignored generated material rebuilt from the existing main generated runtime by prepare.py. No dependencies are installed.

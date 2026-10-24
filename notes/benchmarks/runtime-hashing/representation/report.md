# Local numeric struct scalar replacement

2026-10-23. Third experiment, explicitly requested after the ordered/hash collection reports were complete. **Promising compiler optimization for proven local immutable structs; not a general struct representation replacement.** Actual generated baseline versus an isolated scalar overlay improved 14–36% in two paired runs. No compiler/runtime production edits.

## Actual representation and proposed optimization

`StructFixture.fs` defines `[<Struct>] type Point = { X: int; Y: int }`. Its `localKernel` creates a local point and an updated copy each iteration, reads their fields, and returns only an int. Neither point escapes or enters a function, closure, interface, box or JS boundary. The existing executable identifies itself as **Fickl 0.5.3 / Fable 5.23.0**. It compiled the fixture to `Baseline.generated.js`:

```javascript
export class Point extends Record {
    constructor(X, Y) {
        super(); this.X = (X | 0); this.Y = (Y | 0);
    }
}
// Inside the kernel:
const p = new Point(checksum + i, checksum - i);
const q = new Point(p.Y + 3, p.Y);
checksum = (((q.X ^ p.X) + q.Y) | 0);
```

The measured candidate replaces only those three kernel lines with:

```javascript
const pX = (checksum + i) | 0;
const pY = (checksum - i) | 0;
const qX = (pY + 3) | 0;
const qY = pY;
checksum = (((qX ^ pX) + qY) | 0);
```

The exact diff is `scalar.generated.patch`. All exported types, constructors, reflection functions and other fixture functions remain intact. Integer narrowing that previously occurred in constructors is retained at the corresponding scalar assignment; expression order and unchecked int32 wraparound are preserved. This removes two syntactic object allocations per iteration. No physical allocation/GC-byte claim is made: V8 may already eliminate some baseline allocations.

## FCS information available to a compiler pass

The current main source was inspected independently of the prebuilt compiler:

* `src/Fable.Transforms/FSharp2Fable.Util.fs:503` exposes FCS `ent.FSharpFields`; `:523` exposes `ent.IsValueType`; field adapters at `:73-77` retain field name/type/static/mutable information.
* `FSharp2Fable.Util.fs:1734` preserves `IsStructTupleType` in Fable tuple types; `FSharp2Fable.fs:1496` preserves it on tuple construction. This is distinct from declared-record `IsValueType`; do not invent one universal `IsStruct` flag for all cases.
* `src/Fable.Transforms/Babel/Fable2Babel.fs:1791-1804` lowers `NewRecord` to a constructor call; `:4314` chooses Record as base for F# records/value types, and `:4325-4330` initializes fields with integer wrapping.
* Existing erasure already discards some distinctions: `NewTuple(vals, _)` at `:1757` becomes a JS array; anonymous record `_isStruct` at `:1805` becomes a plain JS object. Direct tuple projection at `:2623-2626` can erase tuple construction. `FableTransforms.fs:210-216` already treats mutable/side-effectful field reads conservatively when inlining.

These metadata plus Fable-AST def-use/escape analysis can prove this narrow kernel eligible without changing the public AST. **FCS knowing a value type alone does not prove non-escape, purity or absence of observable construction.** A prospective pass should initially require immutable numeric fields, known record construction, no user constructor/static initialization effects, local direct field uses only, and no identity/boxing/call/capture/interop use. Preserve evaluation once and in original order even for unused fields. Mutable structs, generic unknown field layouts, byrefs, reflected construction, custom members and arbitrary calls should bail out until separately supported. Scope this optimization to JS/TS initially; shared FableTransforms changes would have cross-target blast radius.

## Correctness and concrete unsafe cases

`check.fsx` ran on .NET, with raw output in `dotnet-results.json`; `compile-results.json` records the actual compiler run. `correctness.json` includes 1,036 baseline/candidate checks across edge/overflow seeds and randomized iteration counts, plus five actual .NET kernel outputs checked against both variants. No mismatch for the optimized numeric kernel.

The fixture also deliberately tests a mutable struct with an int field and an `int ref` field:

```fsharp
let mutable a = { X = 1; Cell = ref 5 }
let mutable b = a
b.X <- 9
b.Cell.Value <- 7
```

Expected shallow value-copy observations `(a.X,b.X,a.Cell.Value,b.Cell.Value)` are **[1,9,7,7]** on .NET. The tested generated JS has `let b = a` and returns **[9,9,7,7]**, identically in baseline and candidate. Reference fields should remain shared while numeric mutable fields are copied; deep cloning would incorrectly break the shared Cell, and sharing the complete wrapper breaks numeric-field value semantics. This is an existing discrepancy in the **tested prebuilt Fickl 0.5.3 / Fable 5.23.0 plus runtime combination**, not proof about a freshly rebuilt latest-main compiler. It is recorded as `fullConformance: false`, not weakened into a passing F# conformance assertion. Our immutable local optimization does not fix it and does not depend on it.

Boxing, interface calls, closure escape and JS object observation are boundary probes, not optimized scenarios:

* `boxPoint` still constructs Point; JS verifies `instanceof Point`, fields X/Y, and existing GetHashCode behavior.
* `interfaceValue` still constructs the Value struct object and accesses the interface getter; returns 6.
* `closurePoint` still captures a materialized Point and returns it with fields 8/9. Capture lifetimes and future mutation/copy semantics are not scalarized.
* JS `Object.keys`, field access and prototype tests expose object shape. The intentionally unsafe escaped scalar `4` has no `.X`; it cannot substitute for the boxed/returned Point.

A general optimizer must materialize the proper object at boxing/interface/unknown-call/escaping-closure/JS boundaries, with correct value-copy and shallow-reference semantics; **that compiler machinery is not implemented or measured here**. A safe first pass can simply decline the entire candidate when any such use appears. Closure capture is not intrinsically impossible to optimize, but it requires a separate proof; no blanket flattening is proposed. Reflection, generic equality/hash/compare, serialization and custom struct methods are further bail-out boundaries until explicitly handled.

## Measurements

Node v26.7.0, Windows; actual compiled baseline and exact scalar overlay imported together. Thirteen alternating ABBA/BAAB blocks, two samples per variant per block, 10,000 warmup calls, sample calibration around 20 ms (2 million maximum calls, 500 minimum), GC before each block, checksums required equal. Exclusive timing slot, compiler/MSBuild workers cleaned before timing. Two fresh-process runs; ordinary workstation noise remains possible.

| Struct iterations per call | First median time change | Repeat |
|---|---:|---:|
| 1 | -14.1% | -14.5% |
| 32 | -36.1% | -34.8% |
| 1,024 | -18.2% | -19.0% |
| 10,000 | -18.0% | -17.8% |

Raw paired ns/call, samples, iteration counts, checksums and observations are in `results.json` and `repeat.json`. Results support pursuing this small pass, not assuming all struct-heavy applications improve by these percentages. No browser, end-to-end application, allocation profiler, cold-start, or optimized compiler implementation was measured. The output is an isolated generated-code experiment, not a production compiler patch.

## Blast radius, provenance and reproduction

An eventual implementation could operate at JS lowering or in a JS-gated Fable optimization pass, initially only on local immutable numeric-record bindings. Public types, escaped representations, runtime APIs and serialized shapes should remain unchanged. Full JS/TS regression tests and targeted F# side-effect/escape/value-copy tests would be required. No edits to Fable.AST or runtime library are needed for this experiment.

`manifest.json` hashes the fixture, prebuilt compiler executable and inspected **current-main** source files separately. These sources are contextual evidence, not proof that the executable was built from exactly that main revision. The generated runtime under main `temp/fable-library-js` remains an environment prerequisite. `Baseline.generated.js` is a retained output snapshot; executable copies live under ignored `temp/` with imports at their original relative depth.

From main repository root, sequentially:

```powershell
rtk proxy python .worktrees/fickle/notes/benchmarks/runtime-hashing/representation/prepare.py
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .worktrees/fickle/notes/benchmarks/cleanup-msbuild.ps1
rtk node .worktrees/fickle/notes/benchmarks/runtime-hashing/representation/benchmark.mjs --check-only
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/representation/benchmark.mjs
rtk node --expose-gc .worktrees/fickle/notes/benchmarks/runtime-hashing/representation/benchmark.mjs repeat.json
```

The five .NET kernel-output assertions were added after timing; the timing body/overlay was unchanged. `correctness.json` includes those checks, whereas original timing files retain the earlier correctness summary. No production files changed, dependencies installed, commits made or next experiment started.

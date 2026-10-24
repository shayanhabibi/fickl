# Experiment 1: nested null options

Completed before the record experiment, 2026-10-23. No production files changed.

The failure is loss of an option layer during code generation, not a hash collision bug. The original generated Fixture.js line 119 represents `[None; Some None; Some (Some null); ...]` as `[undefined, some(undefined), some(defaultOf()), ...]`. `defaultOf()` returns null. The inner `Some null` has been erased, so the second and third items become `Some(undefined)` and `Some(null)`. Runtime equality deliberately treats null and undefined as equal. No equality or hash implementation can reconstruct an erased layer.

Source evidence: `Transforms.Util.fs:1021` only requires wrapping Any, Unit, GenericParam and Option. String is excluded. `Babel/Fable2Babel.fs:1783` therefore emits the inner string option as its payload; line 2632 also erases concrete string-option extraction. Python uses the same predicate at construction (line 508) and extraction (1653), so changing the shared predicate has cross-target implications. This experiment establishes the JS failure; it does not claim Python execution was tested.

## Isolated experiment and results

Run `rtk node notes/benchmarks/runtime-hashing/semantics-fastpaths/options.mjs` from the Fickl worktree. It reads the pre-existing generated fixture/runtime, writes only its own temporary scripts under `temp/semantics-fastpaths`, and saves `options-results.json` here. Prerequisites are documented in the parent README.

The unchanged generated fixture reproduces failures 1814 and 1822: 1,885/1,887. Replacing just `some(defaultOf())` with `some(some(defaultOf()))` in an isolated generated-output copy passes all 1,887 unchanged assertions. The existing .NET baseline is 1,887/1,887 (parent fixture results); this experiment did not rerun .NET. A separate 25-pair matrix verifies all five correctly represented values are distinct, and extraction returns the null payload. All 27 direct assertions pass. The first three values all hash to zero, yet remain distinct under equality: this is a permitted collision, not a defect. No expected F# assertion was weakened.

## Suggested change and guarantees

Investigate a compiler representation fix for nullable concrete payload types, beginning with string. Construction must call `some` where null is possible, and extraction must consistently call `value`; fixing only explicit null literals is insufficient for variables, function results and generic/concrete boundaries. Option tests, uncurrying/optional argument erasure and TS annotations also consult this predicate and need audit. Prefer target-scoped design until other targets are evaluated. Do not change Some.Equals or assign distinct hashes to fix this symptom.

The isolated repair proves sufficiency for this fixture, not a production-ready compiler patch. It preserves nesting, extraction, hash/equality agreement and all collection expectations in the fixture. The runtime already supports the correct representation. Production blast radius includes emitted option ABI and interoperability with precompiled libraries expecting erased string options; mixed compiler/runtime and JS interop deserve explicit compatibility tests. Arrays, declared nullable references, nested generics, map/bind/flatten and dynamically null results need additional coverage. No compiler build, full suite, browser or timing measurement was performed.

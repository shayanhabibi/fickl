# Callable interfaces for generic JavaScript callbacks

Xantham currently hoists method-owned generic parameters when it chooses a delegate for a
generic TypeScript callable. A generic interface Invoke method retains the contract, but an
ordinary F# object expression compiles to an object rather than a JavaScript function.

The Xantham scratch spike verifies imported functions at two type instantiations and a
roundtrip to JavaScript. Direct object-expression construction fails at runtime. A separate
implementation interface plus an adapter that binds its Invoke method works, but duplicates
the public API. InlineInterface addresses tagged-union payload flattening, not this problem.

## Proposed contract

- Add Fickl.Core.CallableInterfaceAttribute for JavaScript and TypeScript interop.
- A supported interface has one instance Invoke method with one argument group; its generic
  parameters stay method-owned. Imported values retain their original function identity.
- Invoking the method emits a direct JavaScript call. Constructing an object expression emits
  a callable function whose arguments and return retain normal Fable conversion rules.
- Prove zero, one and several arguments, generic calls at distinct types, closure capture,
  recursive/self calls, source and DLL consumers, and TypeScript declaration output.
- Reject unsupported shapes explicitly. Do not silently discard additional members, overloads,
  inherited contracts, or unsupported target-language semantics.
- Keep ordinary interfaces unchanged. Do not change Fable.AST's public shape.

## Implementation sequence

1. Add failing small runtime tests for direct generic object-expression construction and calls.
2. Trace construction and call lowering, including equivalent handling in other targets.
3. Implement the attribute and consistent lowering with validation at declaration/use sites.
4. Add negative diagnostics and consumer/reference tests, then run relevant complete suites.
5. Independent review, PR, release, and only then adopt the published compiler in Xantham.

Xantham PR #167 merged as f9660c86b4e8e03fa03046119f229ee92dab0aa1 after all CI passed.
This compiler work is not a substitute
for the remaining overload, missing-owner, required-null and concrete-projection frontiers.

## Working checkpoint

Branch feat/callable-interfaces is based on released Fickl 0.5.4 (64a48c612). The first
regression fails under the released compiler because construction produces an object.
Shared lowering validates declarations, object expressions and calls, and emits direct Invoke
calls with only method-owned generic arguments. Babel lowers the preserved ObjectExpr through
existing method machinery into a named function expression. Keeping MemberRef is essential:
the initial Delegate lowering lost rest-parameter metadata and body-only generic binders.
Independent review found both defects; regressions reproduced them before the corrected
lowering passed. Naming the final function binds self calls without a synthetic wrapper.

TypeScript declarations use a generic call signature. The target-specific Babel AST gains an
AbstractCallSignature case; src/Fable.AST is unchanged. Tests cover outer versus method type
parameters, zero/multiple/rest arguments, generic callback arguments, body-only type binders,
closure capture, recursive self calls, native identity, and a referenced assembly contract.
The quicktest edits have been moved into permanent tests and restored.

Current evidence: 2,944 TypeScript tests and all 149 compiler tests pass. Seven new diagnostic
tests cover unsupported targets, overloads, properties, inherited contracts, curried groups,
secondary object-expression interfaces, and class implementations. The latest independent spec
review found no further actionable issues; standards review's target-test gap is addressed.
The full JavaScript pipeline passes 2,992 .NET checks, 3,178 JavaScript checks, 3,203 standalone
checks and the plugin check. Its log is fickl-callable-js-final.log. Source formatting and
git diff whitespace checks pass.

The integration wrapper still stops on the pre-existing Windows path-map snapshot mismatch
(41/42 integration tests pass); the compiler suite was run directly to bypass that unrelated
wrapper stop. Logs are under Xantham/tests/.scratch/fickl-callable-*.log.

Still required: PR CI and publication, then
adoption through a consumer Xantham slice. No unreleased compiler is pinned in Xantham.

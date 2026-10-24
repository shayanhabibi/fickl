namespace Fickl.Core

open System

/// <summary>
/// Defines a runtime predicate for an erased union case, or an additional test for a
/// <c>TypeScriptTaggedUnion</c> case, checked after the tag matches.
/// </summary>
/// <remarks>
/// The test is a JS expression in <c>Emit</c> syntax, where <c>$0</c> is the tested value:
/// <code>
/// [&lt;TypeScriptTaggedUnion("state")>]
/// type Terminal =
///     | [&lt;CompiledName("done"); CaseTest("'value' in $0")>] Done of value: float
///     | [&lt;CompiledName("done"); CaseTest("'error' in $0")>] DoneError of error: string
///     | Pending
/// </code>
/// On an <c>Erase</c> union, the predicate replaces the payload type test and can distinguish
/// interface or same-type payloads. Construction and extraction still erase to the original
/// payload; no tag is added and no copy is made. The predicate must recognize the values of
/// its case. Cases without this attribute retain their ordinary runtime type tests.
/// <c>Fable.Core.CaseTest</c> retains the payload type test when one exists.
/// </remarks>
type CaseTestAttribute(test: string) =
    inherit Attribute()

/// <summary>
/// Compiles a case of a <c>TypeScriptTaggedUnion</c> as its interface field with the tag added,
/// instead of as an object holding the field.
/// On the union type, it applies to every case that has fields.
/// </summary>
/// <remarks>
/// The case must have exactly one field, whose type is an interface:
/// <code>
/// [&lt;TypeScriptTaggedUnion("type")>]
/// type Command =
///     | [&lt;InlineInterface>] Take of Take
///     | Save
/// </code>
/// The TypeScript type of the <c>Take</c> case is <c>Take &amp; { type: "take" }</c>.
/// Constructing the case copies the field value with its prototype and sets the tag on the copy.
/// Reading the field returns the union value.
/// </remarks>
type InlineInterfaceAttribute() =
    inherit Attribute()

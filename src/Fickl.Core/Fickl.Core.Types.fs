namespace Fickl.Core

open System

/// <summary>
/// Represents a JavaScript callable with a single generic or non-generic Invoke method.
/// </summary>
/// <remarks>
/// Object expressions implementing the interface compile to functions, and Invoke calls
/// compile to direct function calls. Method type parameters remain method-owned.
/// The interface declares one Invoke method with one argument group, or inherits callable
/// interfaces without additional members. Multiple parents must share the same instantiated
/// interface declaring Invoke. This representation targets JavaScript and TypeScript.
/// </remarks>
[<AttributeUsage(AttributeTargets.Interface)>]
type CallableInterfaceAttribute() =
    inherit Attribute()

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

/// <summary>
/// Keeps a function parameter curried in JavaScript, so it is passed and called one argument at a time.
/// </summary>
/// <remarks>
/// Fickl passes a function of several arguments as one JS function taking all of them, <c>(a, b) => ...</c>.
/// A JS API that calls its callback as <c>f(a)(b)</c>, or JS code that passes a curried function to
/// F#, needs the curried shape instead:
/// <code>
/// [&lt;Import("subscribe", "./events.js")>]
/// let subscribe ([&lt;Curried>] handler: string -> int -> unit) : unit = jsNative
/// </code>
/// The parameter is curried both where the member is called and inside its body. On an abstract member,
/// the attribute also applies to every implementation of it.
/// On a record field or a union case field, the field holds its function curried, for a JS object that stores
/// or reads a curried function:
/// <code>
/// type Handlers = { [&lt;Curried>] onKey: string -> int -> unit }
/// </code>
/// </remarks>
[<AttributeUsage(AttributeTargets.Parameter
                 ||| AttributeTargets.Property
                 ||| AttributeTargets.Field)>]
type CurriedAttribute() =
    inherit Attribute()

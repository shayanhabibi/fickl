namespace Fickl.Core

open System

/// <summary>Uses a symbol expression as the JavaScript key of a member, interface slot, or record field.</summary>
/// <remarks>Use a well-known symbol, a stable global expression such as Symbol.for('key'), or an imported symbol.
/// Imported paths are relative to the declaring F# file. The expression must identify the same symbol at every access.
/// Applies to JavaScript and TypeScript; symbol class members attach without AttachMembers.</remarks>
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property ||| AttributeTargets.Field)>]
type SymbolKeyAttribute(expression: string, from: string) =
    inherit Attribute()
    /// <summary>Uses a well-known symbol or stable global symbol expression.</summary>
    new(expression: string) = SymbolKeyAttribute(expression, "")


/// <summary>Emits a module function returning jsAsyncGenerator as a native async generator function.</summary>
/// <remarks>The return type specifies yielded, terminal, and sent values. Supports JavaScript and TypeScript.</remarks>
[<AttributeUsage(AttributeTargets.Method)>]
type AsyncGeneratorFunctionAttribute() =
    inherit Attribute()


/// <summary>Emits a module function returning <c>jsGenerator</c> as a native JavaScript generator function.</summary>
/// <remarks>Supports JavaScript and TypeScript. The return type specifies yielded, terminal, and sent values.</remarks>
[<AttributeUsage(AttributeTargets.Method)>]
type GeneratorFunctionAttribute() =
    inherit Attribute()

/// <summary>Emits a module function returning a <c>jsAsync</c> computation as a native JavaScript async function.</summary>
/// <remarks>The function returns a JavaScript Promise. Applies to JavaScript and TypeScript module functions.</remarks>
[<AttributeUsage(AttributeTargets.Method)>]
type AsyncFunctionAttribute() =
    inherit Attribute()

/// <summary>Compiles literal callbacks passed to this parameter as ordinary JavaScript functions.</summary>
/// <remarks>The callback has its own <c>this</c>, <c>arguments</c>, and prototype.
/// Applies to JavaScript and TypeScript calls; use <c>Fickl.Core.JsInterop.withThis</c> for a typed receiver.</remarks>
[<AttributeUsage(AttributeTargets.Parameter)>]
type JsFunctionAttribute() =
    inherit Attribute()

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
/// How an <c>InlineInterface</c> case holds a field value that does not already carry the case's tag.
/// A field value that already carries the tag is always held as itself.
/// </summary>
type InlineInterfaceRepresentation =
    /// <summary>
    /// A tagged view that forwards properties and methods to the field value.
    /// JavaScript code reading the union value sees the field value's current state.
    /// The union value cannot be structured-cloned, so it cannot cross a <c>postMessage</c> or RPC boundary, and cannot be frozen.
    /// </summary>
    | View = 0
    /// <summary>
    /// A copy of the field value's own enumerable properties, with its prototype and the tag.
    /// The union value is an ordinary object that can be structured-cloned and frozen.
    /// JavaScript code reading the union value sees the state at construction, and its writes do not reach the field value.
    /// </summary>
    | Copy = 1
    /// <summary>
    /// <c>Copy</c> for a field value whose prototype is <c>Object.prototype</c> or <c>null</c>, and <c>View</c> otherwise.
    /// </summary>
    | CopyPlainObjects = 2

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
/// When the field value already carries the case's tag, the case holds the field value itself.
/// Otherwise the tag leaves the field value unchanged, and the <c>InlineInterfaceRepresentation</c>
/// argument chooses how the case holds it; the default is <c>View</c>. An argument on a case overrides one on the union type.
/// Reading the field returns the original object with its identity and state.
/// </remarks>
type InlineInterfaceAttribute(representation: InlineInterfaceRepresentation) =
    inherit Attribute()
    new() = InlineInterfaceAttribute(InlineInterfaceRepresentation.View)
    member _.Representation = representation

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

/// <summary>
/// Drops the overload hash from the compiled name of a member, so JavaScript can call it by a predictable name.
/// </summary>
/// <remarks>
/// Fickl names a method of a class after its type and adds a hash of its parameter types, because JavaScript has
/// no overloading: <c>Counter__Add_Z524259A4</c>. The hash is there even when the method is not overloaded.
/// <code>
/// type Counter() =
///     [&lt;NoOverloadSuffix>]
///     member _.Add(x: int) = x + 1     // Counter__Add
/// </code>
/// On a member that is overloaded, the attribute is an error. On a type, it drops the hash of every member that is
/// not overloaded, and overloaded members keep theirs. On a property, it applies to its getter and setter.
/// </remarks>
[<AttributeUsage(AttributeTargets.Method
                 ||| AttributeTargets.Property
                 ||| AttributeTargets.Constructor
                 ||| AttributeTargets.Class
                 ||| AttributeTargets.Struct)>]
type NoOverloadSuffixAttribute() =
    inherit Attribute()

/// <summary>
/// Writes the TypeScript annotation of this type as the given text, for a type F# cannot describe:
/// conditional, mapped and template literal types, or a library helper type.
/// </summary>
/// <remarks>
/// <c>$0</c>, <c>$1</c>... in the text are the annotations of the type's generic arguments:
/// <code>
/// [&lt;EmitType("Parameters&lt;typeof fetch>[1]")>]
/// type FetchInit = interface end
///
/// [&lt;EmitType("`data-${string}`"); Erase>]
/// type DataAttr = DataAttr of string
///
/// [&lt;EmitType("ComponentProps&lt;typeof $0>", "react")>]
/// type PropsOf&lt;'C> = interface end
/// </code>
/// With a module path, the identifier the text starts with is imported from it as a type, and a relative path
/// is relative to the declaring F# file. The attribute changes only the TypeScript annotations; the JavaScript is the same.
/// An interface or an erased union keeps no TypeScript declaration of its own, and a class keeps its JavaScript class.
/// </remarks>
[<AttributeUsage(AttributeTargets.Class
                 ||| AttributeTargets.Interface
                 ||| AttributeTargets.Struct)>]
type EmitTypeAttribute(typeText: string, from: string) =
    inherit Attribute()
    /// <summary>Writes the annotation as the given text, with no import.</summary>
    new(typeText: string) = EmitTypeAttribute(typeText, "")

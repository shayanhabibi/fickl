namespace Fickl.Core

/// <summary>The TypeScript <c>Partial&lt;T></c>: a <c>'T</c> whose properties may be missing.</summary>
/// <remarks>Build one with <c>JsInterop.partial</c>, and use <c>JsInterop.full</c> to treat it as a complete <c>'T</c>.
/// Changes only TypeScript annotations; the JavaScript value is the object itself.</remarks>
[<EmitType("Partial<$0>")>]
type Partial<'T> = interface end

/// <summary>The TypeScript <c>Required&lt;T></c>: a <c>'T</c> whose optional properties are all present.</summary>
/// <remarks>Changes only TypeScript annotations; the JavaScript value is the object itself.</remarks>
[<EmitType("Required<$0>")>]
type Required<'T> = interface end

/// <summary>The TypeScript <c>Readonly&lt;T></c>: a <c>'T</c> whose properties cannot be assigned.</summary>
/// <remarks>Changes only TypeScript annotations; the JavaScript value is the object itself.</remarks>
[<EmitType("Readonly<$0>")>]
type Readonly<'T> = interface end

/// <summary>Marks the <c>unknown</c> abbreviation for the compiler.</summary>
[<System.AttributeUsage(System.AttributeTargets.All)>]
type internal UnknownTypeAttribute() =
    inherit System.Attribute()

/// <summary>An <c>obj</c> that TypeScript annotations write as <c>unknown</c> instead of <c>any</c>.</summary>
/// <remarks>
/// <c>unknown</c> is <c>obj</c> in F#, so the two convert freely. Where a module function, or an abstract member,
/// takes or returns <c>unknown</c>, its TypeScript signature says <c>unknown</c>, so TypeScript callers must narrow
/// the value before using it. <c>obj</c> stays <c>any</c>:
/// <code>
/// let parse (text: string) : unknown = JS.JSON.parse text   // parse(text: string): unknown
/// </code>
/// A function body still sees <c>any</c>, through an implementation signature after the declared one.
/// Values, fields and properties typed <c>unknown</c> are annotated <c>any</c>. The JavaScript is unchanged.
/// </remarks>
[<UnknownType>]
type unknown = obj

/// <summary>Calls of fluent members marked <c>[&lt;ReturnsThis>]</c>.</summary>
[<RequireQualifiedAccess>]
module Fluent =
    /// <summary>Calls a member that returns its receiver, and types the result as the receiver.</summary>
    /// <remarks>
    /// F# types <c>select.where "x"</c> as the interface declaring <c>where</c>, even when <c>select</c> is a subinterface.
    /// <c>select |> Fluent.chain (fun s -> s.where "x")</c> is the same call typed as <c>select</c>'s own type, so the
    /// subinterface's members stay in reach. The result is cast, which fails on .NET if the member returned another type.
    /// </remarks>
    let inline chain (call: 'T -> 'R) (receiver: 'T) : 'T = call receiver |> unbox<'T>

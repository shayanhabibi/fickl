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

namespace Fickl.Core

#nowarn "1182"

open Fable.Core

/// <summary>Erased intersection type: a value that is both an <c>'a</c> and a <c>'b</c>, the TypeScript <c>A &amp; B</c>.</summary>
/// <remarks>
/// The JavaScript value is the object itself, with no wrapper. Read it as one of its types with <c>As1</c> and
/// <c>As2</c>, or pass it where one of them is expected and it converts implicitly. Build one with <c>UnsafeOf</c>,
/// which does not check that the value is of both types.
/// <code>
/// let describe (x: I2&lt;Named, Aged>) = x.As1.name + string x.As2.age
/// // function describe(x: Named &amp; Aged): string
/// </code>
/// Targets JavaScript and TypeScript.
/// </remarks>
[<Erase; EmitType("$0 & $1")>]
type I2<'a, 'b> =
    private
    | I2 of obj

    /// <summary>The value as an <c>'a</c>.</summary>
    [<Emit("$0")>]
    member _.As1: 'a = nativeOnly

    /// <summary>The value as a <c>'b</c>.</summary>
    [<Emit("$0")>]
    member _.As2: 'b = nativeOnly

    /// <summary>Treats a value as being of both types, unchecked; TypeScript sees the cast.</summary>
    static member UnsafeOf(value: obj) : I2<'a, 'b> = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I2<'a, 'b>) : 'a = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I2<'a, 'b>) : 'b = nativeOnly

/// <summary>Erased intersection type: a value of three types at once, the TypeScript <c>A &amp; B &amp; C</c>.</summary>
/// <remarks>
/// The JavaScript value is the object itself, with no wrapper. Read it as one of its types with <c>As1</c> to
/// <c>As3</c>, or pass it where one of them is expected and it converts implicitly. Build one with <c>UnsafeOf</c>,
/// which does not check that the value is of every type. Targets JavaScript and TypeScript.
/// </remarks>
[<Erase; EmitType("$0 & $1 & $2")>]
type I3<'a, 'b, 'c> =
    private
    | I3 of obj

    /// <summary>The value as an <c>'a</c>.</summary>
    [<Emit("$0")>]
    member _.As1: 'a = nativeOnly

    /// <summary>The value as a <c>'b</c>.</summary>
    [<Emit("$0")>]
    member _.As2: 'b = nativeOnly

    /// <summary>The value as a <c>'c</c>.</summary>
    [<Emit("$0")>]
    member _.As3: 'c = nativeOnly

    /// <summary>Treats a value as being of every type, unchecked; TypeScript sees the cast.</summary>
    static member UnsafeOf(value: obj) : I3<'a, 'b, 'c> = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I3<'a, 'b, 'c>) : 'a = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I3<'a, 'b, 'c>) : 'b = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I3<'a, 'b, 'c>) : 'c = nativeOnly

/// <summary>Erased intersection type: a value of four types at once, the TypeScript <c>A &amp; B &amp; C &amp; D</c>.</summary>
/// <remarks>
/// The JavaScript value is the object itself, with no wrapper. Read it as one of its types with <c>As1</c> to
/// <c>As4</c>, or pass it where one of them is expected and it converts implicitly. Build one with <c>UnsafeOf</c>,
/// which does not check that the value is of every type. Targets JavaScript and TypeScript.
/// </remarks>
[<Erase; EmitType("$0 & $1 & $2 & $3")>]
type I4<'a, 'b, 'c, 'd> =
    private
    | I4 of obj

    /// <summary>The value as an <c>'a</c>.</summary>
    [<Emit("$0")>]
    member _.As1: 'a = nativeOnly

    /// <summary>The value as a <c>'b</c>.</summary>
    [<Emit("$0")>]
    member _.As2: 'b = nativeOnly

    /// <summary>The value as a <c>'c</c>.</summary>
    [<Emit("$0")>]
    member _.As3: 'c = nativeOnly

    /// <summary>The value as a <c>'d</c>.</summary>
    [<Emit("$0")>]
    member _.As4: 'd = nativeOnly

    /// <summary>Treats a value as being of every type, unchecked; TypeScript sees the cast.</summary>
    static member UnsafeOf(value: obj) : I4<'a, 'b, 'c, 'd> = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I4<'a, 'b, 'c, 'd>) : 'a = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I4<'a, 'b, 'c, 'd>) : 'b = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I4<'a, 'b, 'c, 'd>) : 'c = nativeOnly

    [<Emit("$0")>]
    static member op_Implicit(x: I4<'a, 'b, 'c, 'd>) : 'd = nativeOnly

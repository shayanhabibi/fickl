namespace Fickl.Core

/// <summary>Branded primitives: a <c>string</c>, <c>bool</c> or <c>char</c> tagged with a unit of measure, such as
/// <c>string&lt;UserId></c>, so that F# keeps apart values that share a representation.</summary>
/// <remarks>Numbers already take a measure, <c>int&lt;UserId></c>. A branded value is the bare value at runtime and in
/// TypeScript annotations. Tag and untag with the <c>Brand</c> functions, which compile to nothing.</remarks>
/// <example><code lang="fsharp">
/// [&lt;Measure>] type UserId
/// let id: string&lt;UserId> = Brand.tag "u1"   // const id = "u1";
/// let raw: string = Brand.untag id
/// </code></example>
[<AutoOpen>]
module Brands =
    /// <summary>A <c>string</c> tagged with the measure <c>'m</c>.</summary>
    [<MeasureAnnotatedAbbreviation>]
    type string<[<Measure>] 'm> = string

    /// <summary>A <c>bool</c> tagged with the measure <c>'m</c>.</summary>
    [<MeasureAnnotatedAbbreviation>]
    type bool<[<Measure>] 'm> = bool

    /// <summary>A <c>char</c> tagged with the measure <c>'m</c>.</summary>
    [<MeasureAnnotatedAbbreviation>]
    type char<[<Measure>] 'm> = char

/// <summary>Tags primitives with a measure and removes it. Each function returns its argument, and compiles to nothing.</summary>
[<RequireQualifiedAccess>]
module Brand =
    /// <summary>Tags a string with the measure <c>'m</c>.</summary>
    let inline tag<[<Measure>] 'm> (value: string) : string<'m> = unbox value

    /// <summary>Removes the measure from a string.</summary>
    let inline untag (value: string<'m>) : string = unbox value

    /// <summary>Tags a bool with the measure <c>'m</c>.</summary>
    let inline tagBool<[<Measure>] 'm> (value: bool) : bool<'m> = unbox value

    /// <summary>Removes the measure from a bool.</summary>
    let inline untagBool (value: bool<'m>) : bool = unbox value

    /// <summary>Tags a char with the measure <c>'m</c>.</summary>
    let inline tagChar<[<Measure>] 'm> (value: char) : char<'m> = unbox value

    /// <summary>Removes the measure from a char.</summary>
    let inline untagChar (value: char<'m>) : char = unbox value

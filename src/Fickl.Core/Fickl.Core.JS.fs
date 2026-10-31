namespace Fickl.Core.JS

open Fable.Core

/// <summary>A JavaScript array that is only read: the TypeScript <c>ReadonlyArray&lt;T></c>, also written <c>readonly T[]</c>.</summary>
/// <remarks>A parameter of this type accepts a TypeScript <c>readonly T[]</c> or <c>as const</c> array, which a
/// <c>'T[]</c> parameter rejects. Build one from an F# array or <c>ResizeArray</c> with <c>ReadonlyArray.ofArray</c> or
/// <c>ReadonlyArray.ofResizeArray</c>, which compile to nothing in JavaScript.
/// The members are those an F# array has whatever its representation: a numeric F# array is a typed array in
/// JavaScript, which has no <c>map</c> returning another element type, or <c>concat</c>. Use the <c>Seq</c> functions for those.</remarks>
[<Fickl.Core.EmitType("ReadonlyArray<$0>")>]
type ReadonlyArray<'T> =
    inherit seq<'T>
    abstract length: int

    /// <summary>The element at an index.</summary>
    [<EmitIndexer>]
    abstract Item: index: int -> 'T with get

    abstract at: index: int -> 'T option
    abstract includes: searchElement: 'T -> bool
    abstract indexOf: searchElement: 'T -> int
    abstract join: separator: string -> string
    abstract slice: ?start: int * ?``end``: int -> ReadonlyArray<'T>
    abstract find: predicate: ('T -> bool) -> 'T option
    abstract findIndex: predicate: ('T -> bool) -> int
    abstract some: predicate: ('T -> bool) -> bool
    abstract every: predicate: ('T -> bool) -> bool
    abstract forEach: callbackfn: ('T -> unit) -> unit

/// <summary>Conversions to and from <c>ReadonlyArray</c>.</summary>
[<Erase>]
module ReadonlyArray =
    /// <summary>Views an array as read-only, without a copy.</summary>
    /// <param name="array">The array; later changes to it show through the view.</param>
    let ofArray (array: 'T[]) : ReadonlyArray<'T> = nativeOnly

    /// <summary>Views a <c>ResizeArray</c> as read-only, without a copy.</summary>
    /// <param name="list">The list; later changes to it show through the view.</param>
    let ofResizeArray (list: ResizeArray<'T>) : ReadonlyArray<'T> = nativeOnly

    /// <summary>Copies the elements into a new F# array.</summary>
    /// <param name="array">The read-only array to copy.</param>
    let toArray (array: ReadonlyArray<'T>) : 'T[] = nativeOnly

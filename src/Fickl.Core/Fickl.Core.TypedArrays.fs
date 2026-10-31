namespace Fickl.Core

open Fable.Core

/// <summary>Conversions between numeric arrays, which are typed arrays in JavaScript, and an <c>ArrayBuffer</c>.
/// Targets JavaScript and TypeScript.</summary>
/// <remarks>A view of part of an array is an <c>ArraySegment</c>: <c>ArraySegment&lt;byte>(bytes, 4, 16)</c> compiles
/// to <c>bytes.subarray(4, 20)</c>, which shares the array's memory.</remarks>
[<AutoOpen>]
module TypedArrayExtensions =
    type 'T ``[]`` with

        /// <summary>The bytes of a numeric array: its own buffer when the array covers all of it, as an array
        /// F# created does, otherwise a copy of the part it covers.</summary>
        member _.AsArrayBuffer() : JS.ArrayBuffer = nativeOnly

    /// <summary>Numeric arrays over an <c>ArrayBuffer</c>.</summary>
    [<RequireQualifiedAccess>]
    module Array =
        /// <summary>A numeric array over all of a buffer, without a copy: <c>new Uint8Array(buffer)</c> for
        /// <c>byte[]</c>. The buffer's length must be a multiple of the element size.</summary>
        /// <param name="buffer">The buffer to view; writes to either show in the other.</param>
        let ofArrayBuffer (buffer: JS.ArrayBuffer) : 'T[] = nativeOnly

module TypedArrayViews

open System
open Fable.Core
open Fickl.Core

let header (bytes: byte[]) : ArraySegment<byte> = ArraySegment<byte>(bytes, 4, 16)

let whole (values: float[]) = ArraySegment<float>(values)

let rest (segment: ArraySegment<byte>) = segment.Slice(1)

let count (segment: ArraySegment<byte>) = segment.Count + segment.Offset

let first (segment: ArraySegment<int>) = segment.[0]

let bytesOf (buffer: JS.ArrayBuffer) : byte[] = Array.ofArrayBuffer buffer

let floatsOf (buffer: JS.ArrayBuffer) : float[] = Array.ofArrayBuffer buffer

let bufferOf (bytes: byte[]) = bytes.AsArrayBuffer()

let bigUnsigned () : uint64[] = Array.zeroCreate 4

let bigUnsignedView (buffer: JS.ArrayBuffer) = JS.Constructors.BigUint64Array.Create(buffer, 0, 2)

let copyInto (segment: ArraySegment<byte>) (target: byte[]) = segment.CopyTo(target, 1)

let copyOf (segment: ArraySegment<byte>) : byte[] = segment.ToArray()

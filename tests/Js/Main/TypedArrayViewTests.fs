module Fable.Tests.TypedArrayViews

open System
open Fable.Core
open Fickl.Core
open Util.Testing

#if FABLE_COMPILER
[<Emit("$1.constructor.name == $0")>]
let private jsConstructorIs (name: string) (value: obj) = true

[<Emit("$0.buffer === $1.buffer")>]
let private sharesBuffer (a: obj) (b: obj) = true
#endif

let tests =
    testList "TypedArrayViews" [
        testCase "ArraySegment reads part of an array" <| fun () ->
            let bytes = [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy |]
            let segment = ArraySegment<byte>(bytes, 2, 5)
            equal 5 segment.Count
            equal 2 segment.Offset
            equal 2uy segment.[0]
            equal 6uy segment.[4]
            equal [| 2uy; 3uy; 4uy; 5uy; 6uy |] (segment.ToArray())
            equal 20 (Seq.sum (segment |> Seq.map int))

        testCase "ArraySegment shares memory with its array" <| fun () ->
            let values = Array.zeroCreate<float> 4
            let mutable segment = ArraySegment<float>(values, 1, 2)
            segment.[0] <- 5.
            values.[2] <- 7.
            equal 5. values.[1]
            equal 7. segment.[1]

        testCase "ArraySegment over a whole array" <| fun () ->
            let values = [| 1; 2; 3 |]
            let segment = ArraySegment<int>(values)
            equal 3 segment.Count
            equal 0 segment.Offset
            equal [| 1; 2; 3 |] (segment.ToArray())

        testCase "ArraySegment.Slice is a view of the segment" <| fun () ->
            let values = [| 0; 1; 2; 3; 4; 5; 6; 7; 8; 9 |]
            let segment = ArraySegment<int>(values, 2, 6)
            let mutable slice = segment.Slice(1, 3)
            equal [| 3; 4; 5 |] (slice.ToArray())
            equal [| 3; 4; 5; 6; 7 |] (segment.Slice(1).ToArray())
            slice.[0] <- 30
            equal 30 values.[3]

        testCase "ArraySegment.CopyTo copies into an array" <| fun () ->
            let segment = ArraySegment<byte>([| 1uy; 2uy; 3uy; 4uy; 5uy |], 1, 3)
            let target = Array.zeroCreate<byte> 5
            segment.CopyTo(target)
            equal [| 2uy; 3uy; 4uy; 0uy; 0uy |] target
            segment.CopyTo(target, 2)
            equal [| 2uy; 3uy; 2uy; 3uy; 4uy |] target

#if FABLE_COMPILER
        testCase "uint64 arrays are BigUint64Arrays" <| fun () ->
            jsConstructorIs "BigUint64Array" (Array.zeroCreate<uint64> 2) |> equal true
            let fromConstructor = JS.Constructors.BigUint64Array.Create(2)
            fromConstructor.[1] <- 18446744073709551615UL
            equal 18446744073709551615UL fromConstructor.[1]

        testCase "ArraySegment compiles to a subarray of the same type" <| fun () ->
            let bytes = [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy |]
            let segment = ArraySegment<byte>(bytes, 4, 4)
            jsConstructorIs "Uint8Array" segment |> equal true
            sharesBuffer segment bytes |> equal true

        testCase "Array.ofArrayBuffer views a buffer without a copy" <| fun () ->
            let floats = [| 1.; 2. |]
            let bytes: byte[] = Array.ofArrayBuffer floats.buffer
            equal 16 bytes.Length
            sharesBuffer bytes floats |> equal true
            let again: float[] = Array.ofArrayBuffer floats.buffer
            equal [| 1.; 2. |] again

        testCase "AsArrayBuffer gives an array's own buffer" <| fun () ->
            let bytes = [| 1uy; 2uy; 3uy |]
            equal true (obj.ReferenceEquals(bytes.AsArrayBuffer(), bytes.buffer))
            equal 3 (bytes.AsArrayBuffer().byteLength)

        testCase "AsArrayBuffer copies the part a view covers" <| fun () ->
            let bytes = [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy |]
            let view: byte[] = unbox (box (ArraySegment<byte>(bytes, 2, 3)))
            let buffer = view.AsArrayBuffer()
            equal 3 buffer.byteLength
            equal false (obj.ReferenceEquals(buffer, bytes.buffer))
            let copy: byte[] = Array.ofArrayBuffer buffer
            equal [| 2uy; 3uy; 4uy |] copy
#endif
    ]

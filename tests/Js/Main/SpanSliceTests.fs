module Fable.Tests.SpanSlices

open System
open Util.Testing
open Fable.Tests.Util

// A slice of a span or a memory over a numeric array is a view of a typed array, so these tests need typed arrays

let tests =
  testList "SpanSlices" [
    testCase "A slice of a span over a numeric array shares its memory" <| fun () ->
        let xs = [| 1; 2; 3; 4; 5 |]
        let span = Span<int>(xs)
        let middle = span.Slice(1, 3)
        middle.Length |> equal 3
        middle[0] |> equal 2
        middle[0] <- 20
        xs[1] |> equal 20
        let tail = span.Slice(3)
        tail.Length |> equal 2
        tail[1] |> equal 5
        let fromCtor = Span<int>(xs, 2, 2)
        fromCtor[0] |> equal 3
        fromCtor.Length |> equal 2

    testCase "AsSpan over part of an array shares its memory" <| fun () ->
        let xs = [| 1; 2; 3; 4; 5 |]
        let part = xs.AsSpan(1, 2)
        part.Length |> equal 2
        part[0] |> equal 2
        part[1] <- 30
        xs[2] |> equal 30
        xs.AsSpan(3).Length |> equal 2

    testCase "A slice of a memory over a numeric array works" <| fun () ->
        let memory = ReadOnlyMemory<int>([| 1; 2; 3; 4 |])
        memory.Slice(1, 2).ToArray() |> equal [| 2; 3 |]
        memory.Slice(2).Length |> equal 2
        memory.Slice(1, 2).Span[0] |> equal 2
  ]

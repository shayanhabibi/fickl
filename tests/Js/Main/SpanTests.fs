module Fable.Tests.Spans

open System
open System.Collections.Generic
open System.Globalization
open System.Text.RegularExpressions
open Util.Testing
open Fable.Tests.Util

// Span, ReadOnlySpan, Memory and ReadOnlyMemory over arrays and strings

type private Point = { X: int; Y: int }

type private Summer =
    static member Sum(values: ReadOnlySpan<int>) =
        let mutable total = 0

        for i in 0 .. values.Length - 1 do
            total <- total + values[i]

        total

    static member Names(points: ReadOnlySpan<Point>) =
        let mutable text = ""

        for i in 0 .. points.Length - 1 do
            text <- text + string points[i].X + ":" + string points[i].Y + ";"

        text

    static member Fill(target: Span<float>, value: float) =
        for i in 0 .. target.Length - 1 do
            target[i] <- value

let tests =
  testList "Spans" [
    testCase "ReadOnlySpan over an array has its length and elements" <| fun () ->
        let xs = [| 1; 2; 3; 4 |]
        let span = ReadOnlySpan<int>(xs)
        span.Length |> equal 4
        span.IsEmpty |> equal false
        span[0] |> equal 1
        span[3] |> equal 4

    testCase "A ReadOnlySpan passes as an argument" <| fun () ->
        Summer.Sum(ReadOnlySpan<int>([| 1; 2; 3; 4 |])) |> equal 10
        Summer.Sum(ReadOnlySpan<int>([| 5; 6 |])) |> equal 11

    testCase "ReadOnlySpan over an array of records works" <| fun () ->
        Summer.Names(ReadOnlySpan<Point>([| { X = 1; Y = 2 }; { X = 3; Y = 4 } |])) |> equal "1:2;3:4;"

    testCase "Span writes reach the array" <| fun () ->
        let xs = [| 1.0; 2.0; 3.0 |]
        let span = Span<float>(xs)
        span[1] <- 20.0
        xs[1] |> equal 20.0
        Summer.Fill(Span<float>(xs), 7.0)
        xs |> equal [| 7.0; 7.0; 7.0 |]

    testCase "Span ToArray copies" <| fun () ->
        let xs = [| 1; 2; 3 |]
        let copy = ReadOnlySpan<int>(xs).ToArray()
        copy |> equal [| 1; 2; 3 |]
        xs[0] <- 10
        copy[0] |> equal 1

    testCase "An empty span has no elements" <| fun () ->
        let empty = ReadOnlySpan<int>.Empty
        empty.Length |> equal 0
        empty.IsEmpty |> equal true
        ReadOnlySpan<int>([||]).IsEmpty |> equal true

    testCase "AsSpan over an array works" <| fun () ->
        let xs = [| 1; 2; 3; 4; 5 |]
        let all = xs.AsSpan()
        all.Length |> equal 5
        all[1] <- 20
        xs[1] |> equal 20

    testCase "AsSpan over a string works" <| fun () ->
        let text = "hello world"
        let all = text.AsSpan()
        all.Length |> equal 11
        all[1] |> equal 'e'
        text.AsSpan(6).ToString() |> equal "world"
        text.AsSpan(6, 3).ToString() |> equal "wor"
        text.AsSpan(0, 5).Length |> equal 5

    testCase "A slice of a character span works" <| fun () ->
        let span = "hello world".AsSpan()
        span.Slice(6).ToString() |> equal "world"
        span.Slice(0, 5).ToString() |> equal "hello"
        span.Slice(2, 3).ToArray() |> equal [| 'l'; 'l'; 'o' |]

    testCase "AsSpan evaluates its start once" <| fun () ->
        let mutable calls = 0

        let next () =
            calls <- calls + 1
            1

        "abcdef".AsSpan(next (), 2).ToString() |> equal "bc"
        calls |> equal 1

    testCase "Numbers parse from a character span" <| fun () ->
        Int32.Parse("x=42".AsSpan(2)) |> equal 42
        Byte.Parse("ff".AsSpan(), NumberStyles.HexNumber, CultureInfo.InvariantCulture) |> equal 255uy
        Byte.Parse("#1a2b".AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) |> equal 26uy
        Double.Parse("v1.5".AsSpan(1), CultureInfo.InvariantCulture) |> equal 1.5

    testCase "ReadOnlyMemory over an array works" <| fun () ->
        let xs = [| 1; 2; 3; 4 |]
        let memory = ReadOnlyMemory<int>(xs)
        memory.Length |> equal 4
        memory.IsEmpty |> equal false
        memory.Span[2] |> equal 3
        Summer.Sum memory.Span |> equal 10
        ReadOnlyMemory<int>.Empty.Length |> equal 0

    testCase "ReadOnlyMemory over an array of records works" <| fun () ->
        let memory = ReadOnlyMemory<Point>([| { X = 1; Y = 2 } |])
        memory.Length |> equal 1
        memory.Span[0].Y |> equal 2
        Summer.Names memory.Span |> equal "1:2;"

    testCase "Memory writes reach the array" <| fun () ->
        let xs = [| 1.0; 2.0 |]
        let memory = Memory<float>(xs)
        Summer.Fill(memory.Span, 3.0)
        xs |> equal [| 3.0; 3.0 |]

    testCase "StringComparer.Ordinal compares by code unit" <| fun () ->
        StringComparer.Ordinal.Equals("a", "a") |> equal true
        StringComparer.Ordinal.Equals("a", "A") |> equal false
        StringComparer.Ordinal.Compare("a", "b") < 0 |> equal true
        StringComparer.Ordinal.Compare("b", "a") > 0 |> equal true
        StringComparer.Ordinal.Compare("a", "a") |> equal 0
        StringComparer.Ordinal.Compare("B", "a") < 0 |> equal true

    testCase "A HashSet with StringComparer.Ordinal works" <| fun () ->
        let names = HashSet<string>(StringComparer.Ordinal)
        names.Add "vs_main" |> equal true
        names.Add "vs_main" |> equal false
        names.Contains "vs_main" |> equal true
        names.Contains "VS_MAIN" |> equal false
        names.Count |> equal 1
        let seeded = HashSet<string>([ "a"; "b"; "a" ], StringComparer.Ordinal)
        seeded.Count |> equal 2

    testCase "A Dictionary with StringComparer.Ordinal works" <| fun () ->
        let ages = Dictionary<string, int>(StringComparer.Ordinal)
        ages["ann"] <- 1
        ages["Ann"] <- 2
        ages.Count |> equal 2
        ages["ann"] |> equal 1
        ages.ContainsKey "ANN" |> equal false

    testCase "RegexOptions.CultureInvariant is accepted" <| fun () ->
        Regex("a+", RegexOptions.CultureInvariant).IsMatch("caat") |> equal true
        let pattern = Regex(@"\bfn\s+([a-z_]+)", RegexOptions.CultureInvariant ||| RegexOptions.IgnoreCase)
        pattern.Match("FN vs_main()").Groups[1].Value |> equal "vs_main"
        Regex.Replace("a//b\nc", @"//[^\n]*", " ", RegexOptions.CultureInvariant) |> equal "a \nc"
  ]

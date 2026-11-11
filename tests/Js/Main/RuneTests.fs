module Fable.Tests.Runes

open System
open System.Collections.Generic
open System.Globalization
open System.Text
open Util.Testing
open Fable.Tests.Util

// System.Text.Rune

type RuneHolder = { Rune: Rune; Name: string }

let private highSurrogate = char 0xD83D
let private lowSurrogate = char 0xDE00
let private grinning = "😀" // U+1F600

let private throwsOutOfRange (f: unit -> 'a) =
    let mutable caught = false

    try
        f () |> ignore
    with :? ArgumentOutOfRangeException ->
        caught <- true

    equal true caught

let tests =
    testList "Runes" [
        testCase "Rune(char) works" <| fun () ->
            Rune('A').Value |> equal 65
            let c = 'é'
            Rune(c).Value |> equal 0xE9

        testCase "Rune(char) throws for a surrogate" <| fun () ->
            throwsOutOfRange (fun () -> Rune(highSurrogate))
            throwsOutOfRange (fun () -> Rune(lowSurrogate))

        testCase "Rune(int) works" <| fun () ->
            Rune(0x1F600).Value |> equal 0x1F600
            Rune(0).Value |> equal 0
            Rune(0x10FFFF).Value |> equal 0x10FFFF
            Rune(0xD7FF).Value |> equal 0xD7FF
            Rune(0xE000).Value |> equal 0xE000

        testCase "Rune(int) throws for a value that is not a scalar" <| fun () ->
            throwsOutOfRange (fun () -> Rune(-1))
            throwsOutOfRange (fun () -> Rune(0xD800))
            throwsOutOfRange (fun () -> Rune(0xDFFF))
            throwsOutOfRange (fun () -> Rune(0x110000))

        testCase "Rune(uint32) works" <| fun () ->
            Rune(0x1F600u).Value |> equal 0x1F600
            throwsOutOfRange (fun () -> Rune(0xD800u))
            throwsOutOfRange (fun () -> Rune(0xFFFFFFFFu))

        testCase "Rune(char, char) works" <| fun () ->
            Rune(highSurrogate, lowSurrogate).Value |> equal 0x1F600
            throwsOutOfRange (fun () -> Rune('a', 'b'))
            throwsOutOfRange (fun () -> Rune(lowSurrogate, highSurrogate))
            throwsOutOfRange (fun () -> Rune(highSurrogate, 'a'))

        testCase "Rune default value is U+0000" <| fun () ->
            Rune().Value |> equal 0
            Unchecked.defaultof<Rune>.Value |> equal 0

        testCase "Rune explicit conversions work" <| fun () ->
            (Rune.op_Explicit 'A': Rune).Value |> equal 65
            (Rune.op_Explicit 0x1F600: Rune).Value |> equal 0x1F600
            throwsOutOfRange (fun () -> (Rune.op_Explicit 0xD800: Rune))

        testCase "Rune.TryCreate works" <| fun () ->
            let ok, rune = Rune.TryCreate(0x1F600)
            equal true ok
            rune.Value |> equal 0x1F600
            let ok, rune = Rune.TryCreate(0xD800)
            equal false ok
            rune.Value |> equal 0
            Rune.TryCreate(-1) |> fst |> equal false
            Rune.TryCreate(0x110000u) |> fst |> equal false
            Rune.TryCreate(0x10FFFFu) |> equal (true, Rune(0x10FFFF))
            Rune.TryCreate('A') |> equal (true, Rune('A'))
            Rune.TryCreate(highSurrogate) |> equal (false, Rune(0))
            Rune.TryCreate(highSurrogate, lowSurrogate) |> equal (true, Rune(0x1F600))
            Rune.TryCreate('a', 'b') |> equal (false, Rune(0))

        testCase "Rune.IsValid works" <| fun () ->
            [ -1; 0; 0xD7FF; 0xD800; 0xDFFF; 0xE000; 0x10FFFF; 0x110000 ]
            |> List.map Rune.IsValid
            |> equal [ false; true; true; false; false; true; true; false ]
            Rune.IsValid(0xFFFFFFFFu) |> equal false
            Rune.IsValid(0x1F600u) |> equal true

        testCase "Rune.GetRuneAt works" <| fun () ->
            let s = "a" + grinning + "b"
            Rune.GetRuneAt(s, 0).Value |> equal 97
            Rune.GetRuneAt(s, 1).Value |> equal 0x1F600
            Rune.GetRuneAt(s, 3).Value |> equal 98
            Rune.GetRuneAt("ABC", 1).ToString() |> equal "B"

        testCase "Rune.GetRuneAt throws inside a surrogate pair and for a lone surrogate" <| fun () ->
            let s = "a" + grinning + "b"
            throwsAnyError (fun () -> Rune.GetRuneAt(s, 2))
            throwsAnyError (fun () -> Rune.GetRuneAt(string highSurrogate + "a", 0))
            throwsAnyError (fun () -> Rune.GetRuneAt(string highSurrogate, 0))
            throwsAnyError (fun () -> Rune.GetRuneAt(string lowSurrogate, 0))

        testCase "Rune.GetRuneAt throws when the index is out of range" <| fun () ->
            throwsOutOfRange (fun () -> Rune.GetRuneAt("abc", -1))
            throwsOutOfRange (fun () -> Rune.GetRuneAt("abc", 3))
            throwsOutOfRange (fun () -> Rune.GetRuneAt("", 0))

        testCase "Rune.TryGetRuneAt works" <| fun () ->
            let s = "a" + grinning + "b"
            Rune.TryGetRuneAt(s, 1) |> equal (true, Rune(0x1F600))
            Rune.TryGetRuneAt(s, 2) |> equal (false, Rune(0))
            let ok, rune = Rune.TryGetRuneAt(s, 3)
            equal true ok
            rune.Value |> equal 98
            throwsOutOfRange (fun () -> Rune.TryGetRuneAt("abc", 3))
            throwsOutOfRange (fun () -> Rune.TryGetRuneAt("abc", -1))

        testCase "Rune sequence lengths, IsAscii, IsBmp and Plane work" <| fun () ->
            [ 0x41; 0x7F; 0x80; 0x7FF; 0x800; 0xFFFF; 0x10000; 0x1F600; 0x10FFFF ]
            |> List.map (fun cp ->
                let r = Rune cp
                r.Utf16SequenceLength, r.Utf8SequenceLength, r.IsAscii, r.IsBmp, r.Plane)
            |> equal [
                1, 1, true, true, 0
                1, 1, true, true, 0
                1, 2, false, true, 0
                1, 2, false, true, 0
                1, 3, false, true, 0
                1, 3, false, true, 0
                2, 4, false, false, 1
                2, 4, false, false, 1
                2, 4, false, false, 16
            ]

        testCase "Rune.ReplacementChar works" <| fun () ->
            Rune.ReplacementChar.Value |> equal 0xFFFD
            Rune.ReplacementChar.ToString() |> equal "�"

        testCase "Rune.ToString gives the character" <| fun () ->
            Rune('A').ToString() |> equal "A"
            Rune(0x1F600).ToString() |> equal grinning
            Rune(0x1F600).ToString().Length |> equal 2

        testCase "string and interpolation of a Rune give the character" <| fun () ->
            let r = Rune(0x1F600)
            string r |> equal grinning
            $"{r}!" |> equal (grinning + "!")
            $"""{Rune('A'),3}|""" |> equal "  A|"
            $"%s{string r}%d{r.Value}" |> equal (grinning + "128512")
            string (Some(Rune('A'))) |> equal "Some(A)"
            string (Rune('A'), 1) |> equal "(A, 1)"

        testCase "Rune equality and comparison work" <| fun () ->
            Rune(65) = Rune('A') |> equal true
            Rune(65) <> Rune(66) |> equal true
            Rune(65) < Rune(66) |> equal true
            Rune(0x1F600) > Rune(0xFFFF) |> equal true
            compare (Rune 0x1F600) (Rune 65) > 0 |> equal true
            Rune(65).CompareTo(Rune(66)) |> equal -1
            Rune(65).Equals(Rune(65)) |> equal true
            Rune(65).Equals(Rune(66)) |> equal false
            max (Rune 65) (Rune 66) |> equal (Rune 66)
            Rune(65).GetHashCode() = Rune('A').GetHashCode() |> equal true
            hash (Rune 0x1F600) = hash (Rune 0x1F600) |> equal true
            Rune.op_LessThan(Rune 1, Rune 2) |> equal true
            Rune.op_LessThanOrEqual(Rune 2, Rune 2) |> equal true
            Rune.op_GreaterThan(Rune 1, Rune 2) |> equal false
            Rune.op_GreaterThanOrEqual(Rune 1, Rune 2) |> equal false
            Rune.op_Equality(Rune 1, Rune 1) |> equal true
            Rune.op_Inequality(Rune 1, Rune 1) |> equal false

        testCase "Rune works in records and collections" <| fun () ->
            { Rune = Rune 65; Name = "a" } = { Rune = Rune 'A'; Name = "a" } |> equal true
            [ Rune 0x1F600; Rune 66; Rune 65 ] |> List.sort |> List.map _.Value |> equal [ 65; 66; 0x1F600 ]
            let counts = Dictionary<Rune, int>()

            for r in ("ab" + grinning + "a" + grinning).EnumerateRunes() do
                counts[r] <- (match counts.TryGetValue r with
                              | true, n -> n + 1
                              | _ -> 1)

            counts[Rune 'a'] |> equal 2
            counts[Rune 0x1F600] |> equal 2
            counts.Count |> equal 3
            let set = HashSet<Rune>([ Rune 65; Rune 'A'; Rune 66 ])
            set.Count |> equal 2
            Set.ofList [ Rune 66; Rune 65; Rune 66 ] |> Set.toList |> equal [ Rune 65; Rune 66 ]
            Map.ofList [ Rune 65, "A" ] |> Map.find (Rune 'A') |> equal "A"

        testCase "String.EnumerateRunes works in a for loop" <| fun () ->
            let values = ResizeArray()

            for r in ("a" + grinning + "b").EnumerateRunes() do
                values.Add r.Value

            values |> Seq.toList |> equal [ 97; 0x1F600; 98 ]
            let mutable count = 0

            for _ in "".EnumerateRunes() do
                count <- count + 1

            count |> equal 0

        testCase "String.EnumerateRunes works as a sequence" <| fun () ->
            let s = "a" + grinning + "b"
            s.EnumerateRunes() |> Seq.map _.Value |> Seq.toList |> equal [ 97; 0x1F600; 98 ]
            s.EnumerateRunes() |> Seq.map string |> Seq.toList |> equal [ "a"; grinning; "b" ]
            [ for r in s.EnumerateRunes() -> r.Utf16SequenceLength ] |> equal [ 1; 2; 1 ]
            s.EnumerateRunes() |> Seq.length |> equal 3
            let runes = s.EnumerateRunes()
            Seq.toArray runes |> equal (Seq.toArray runes)

        testCase "String.EnumerateRunes enumerator works" <| fun () ->
            let mutable e = ("a" + grinning).EnumerateRunes().GetEnumerator()
            e.MoveNext() |> equal true
            e.Current.Value |> equal 97
            e.MoveNext() |> equal true
            e.Current.Value |> equal 0x1F600
            e.MoveNext() |> equal false

        testCase "String.EnumerateRunes replaces lone surrogates with U+FFFD" <| fun () ->
            let values (s: string) =
                [ for r in s.EnumerateRunes() -> r.Value ]

            string highSurrogate + "A" + string lowSurrogate |> values |> equal [ 0xFFFD; 65; 0xFFFD ]
            string lowSurrogate + string highSurrogate |> values |> equal [ 0xFFFD; 0xFFFD ]
            string highSurrogate + grinning |> values |> equal [ 0xFFFD; 0x1F600 ]

        testCase "Rune classifiers work" <| fun () ->
            let classify (cp: int) =
                let r = Rune cp

                [
                    Rune.IsLetter r
                    Rune.IsDigit r
                    Rune.IsWhiteSpace r
                    Rune.IsUpper r
                    Rune.IsLower r
                    Rune.IsLetterOrDigit r
                    Rune.IsNumber r
                    Rune.IsPunctuation r
                    Rune.IsSeparator r
                    Rune.IsSymbol r
                    Rune.IsControl r
                ]
                |> List.map (fun b -> if b then "1" else "0")
                |> String.concat ""

            classify 0x41 |> equal "10010100000"
            classify 0x61 |> equal "10001100000"
            classify 0x35 |> equal "01000110000"
            classify 0x20 |> equal "00100000100"
            classify 0x09 |> equal "00100000001"
            classify 0x85 |> equal "00100000001"
            classify 0xA0 |> equal "00100000100"
            classify 0x2028 |> equal "00100000100"
            classify 0x21 |> equal "00000001000"
            classify 0x2B |> equal "00000000010"
            classify 0x1F600 |> equal "00000000010"
            classify 0x10400 |> equal "10010100000"
            classify 0x10428 |> equal "10001100000"
            classify 0x1D7D8 |> equal "01000110000"
            classify 0x20000 |> equal "10000100000"
            classify 0x10FFFF |> equal "00000000000"

        testCase "Rune.GetUnicodeCategory works" <| fun () ->
            Rune.GetUnicodeCategory(Rune 'A') |> equal UnicodeCategory.UppercaseLetter
            Rune.GetUnicodeCategory(Rune 0x1F600) |> equal UnicodeCategory.OtherSymbol
            Rune.GetUnicodeCategory(Rune 0x10428) |> equal UnicodeCategory.LowercaseLetter
            Rune.GetUnicodeCategory(Rune 0x20000) |> equal UnicodeCategory.OtherLetter
            Rune.GetUnicodeCategory(Rune 0xE0001) |> equal UnicodeCategory.Format
            Rune.GetUnicodeCategory(Rune 0x10FFFF) |> equal UnicodeCategory.OtherNotAssigned

        testCase "Rune.ToUpperInvariant and ToLowerInvariant work" <| fun () ->
            Rune.ToUpperInvariant(Rune 'a') |> equal (Rune 'A')
            Rune.ToLowerInvariant(Rune 'A') |> equal (Rune 'a')
            Rune.ToUpperInvariant(Rune 0x10428).Value |> equal 0x10400
            Rune.ToLowerInvariant(Rune 0x10400).Value |> equal 0x10428
            Rune.ToUpperInvariant(Rune 0x1F600).Value |> equal 0x1F600
            // A mapping that would expand is left unchanged
            Rune.ToUpperInvariant(Rune 'ß') |> equal (Rune 'ß')
            Rune.ToLowerInvariant(Rune 0x130).Value |> equal 0x130
            Rune.ToUpperInvariant(Rune 0x1C5).Value |> equal 0x1C4
            Rune.ToLowerInvariant(Rune 0x1C5).Value |> equal 0x1C6
    ]

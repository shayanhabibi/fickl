module Fable.Tests.Decimals

open System
open Util.Testing

// System.Decimal: every expected value is what .NET 10 gives

let private bits (d: decimal) =
    String.Join(",", Decimal.GetBits d |> Array.map string)

// The bits show the scale and the sign apart from the digits
let private sb (d: decimal) = d.ToString() + " [" + bits d + "]"

let private thrown (f: unit -> 'a) =
    try
        f () |> ignore
        "no exception"
    with
    | :? OverflowException -> "OverflowException"
    | :? DivideByZeroException -> "DivideByZeroException"
    | :? ArgumentOutOfRangeException -> "ArgumentOutOfRangeException"
    | :? FormatException -> "FormatException"
    | e -> "Unexpected exception: " + e.Message

let tests =
    testList "Decimals" [
        testCase "Addition and subtraction keep the larger scale" <| fun () ->
            sb (1.5m + 1.50m) |> equal "3.00 [300,0,0,131072]"
            sb (0.1m + 0.2m) |> equal "0.3 [3,0,0,65536]"
            sb (1m - 1.00m) |> equal "0.00 [0,0,0,131072]"
            sb (1.5m - 1.50m) |> equal "0.00 [0,0,0,131072]"
            sb (0.0m - 0.00m) |> equal "0.00 [0,0,0,-2147352576]"
            sb (-1.5m + 1.50m) |> equal "0.00 [0,0,0,-2147352576]"
            sb (100m + 0.000001m) |> equal "100.000001 [100000001,0,0,393216]"
            sb (-0.5m - 0.25m) |> equal "-0.75 [75,0,0,-2147352576]"

        testCase "Addition and subtraction round half to even when 96 bits overflow" <| fun () ->
            sb (Decimal.MaxValue + 0.4m) |> equal "79228162514264337593543950335 [-1,-1,-1,0]"
            sb (Decimal.MaxValue + 0.49m) |> equal "79228162514264337593543950335 [-1,-1,-1,0]"
            sb (Decimal.MaxValue - 0.5m) |> equal "79228162514264337593543950334 [-2,-1,-1,0]"
            sb (Decimal.MaxValue - 0.25m) |> equal "79228162514264337593543950335 [-1,-1,-1,0]"
            sb (1.005m + 7922816251426433759354395033.5m) |> equal "7922816251426433759354395035 [-1717986917,-1717986919,429496729,0]"
            sb (19.99m + 7922816251426433759354395033.5m) |> equal "7922816251426433759354395053 [-1717986899,-1717986919,429496729,0]"
            sb (7922816251426433759354395033.5m + 99999999999999.9999m) |> equal "7922816251426533759354395033 [-1441539687,-1717963636,429496729,0]"
            sb (7922816251426433759354395033.5m - 0.0000000000000000000000000001m) |> equal "7922816251426433759354395033.5 [-1,-1,-1,65536]"

        testCase "Multiplication adds the scales" <| fun () ->
            sb (1.1m * 1.1m) |> equal "1.21 [121,0,0,131072]"
            sb (1.10m * 1.10m) |> equal "1.2100 [12100,0,0,262144]"
            sb (-1.5m * 2.00m) |> equal "-3.000 [3000,0,0,-2147287040]"
            sb (0m * 1.00m) |> equal "0.00 [0,0,0,131072]"
            sb (0.0m * -1.00m) |> equal "0.000 [0,0,0,-2147287040]"
            sb (0.0m * 12345678901234567890.12345678m) |> equal "0 [0,0,0,0]"
            sb (0.0000000000000000000000000001m * 0.5m) |> equal "0.0000000000000000000000000000 [0,0,0,1835008]"
            sb (0.0000000000000000000000000003m * 0.5m) |> equal "0.0000000000000000000000000002 [2,0,0,1835008]"
            sb (0.0000000000000000000000000001m * 0.1m) |> equal "0.0000000000000000000000000000 [0,0,0,1835008]"
            sb (0.000000000000001m * 0.000000000000001m) |> equal "0.0000000000000000000000000000 [0,0,0,1835008]"
            sb (Decimal.MaxValue * 1.0m) |> equal "79228162514264337593543950335 [-1,-1,-1,0]"
            sb (Decimal.MaxValue * 0.1m) |> equal "7922816251426433759354395033.5 [-1,-1,-1,65536]"
            sb (0.3333333333333333333333333333m * 3m) |> equal "0.9999999999999999999999999999 [268435455,1042612833,542101086,1835008]"
            sb (1.0000000000000000000000000001m * 1.0000000000000000000000000001m) |> equal "1.0000000000000000000000000002 [268435458,1042612833,542101086,1835008]"
            sb (12345678901234567890.12345678m * 99999999.9999m) |> equal "1234567890122222221122222221.2 [281949572,1184034141,669260594,65536]"

        testCase "Exact division keeps the difference of the scales" <| fun () ->
            sb (1m / 8m) |> equal "0.125 [125,0,0,196608]"
            sb (1.0m / 8m) |> equal "0.125 [125,0,0,196608]"
            sb (1.0000m / 8m) |> equal "0.1250 [1250,0,0,262144]"
            sb (1.00m / 1.0m) |> equal "1.0 [10,0,0,65536]"
            sb (1.0m / 1.00m) |> equal "1 [1,0,0,0]"
            sb (6.00m / 2m) |> equal "3.00 [300,0,0,131072]"
            sb (6m / 2.00m) |> equal "3 [3,0,0,0]"
            sb (0.00m / 3m) |> equal "0.00 [0,0,0,131072]"
            sb (0m / 3.00m) |> equal "0 [0,0,0,0]"
            sb (-6.00m / 2m) |> equal "-3.00 [300,0,0,-2147352576]"
            sb (1m / 0.0000000000000000000000000001m) |> equal "10000000000000000000000000000 [268435456,1042612833,542101086,0]"

        testCase "Inexact division rounds half to even and strips trailing zeros" <| fun () ->
            sb (1m / 3m) |> equal "0.3333333333333333333333333333 [89478485,347537611,180700362,1835008]"
            sb (2m / 3m) |> equal "0.6666666666666666666666666667 [178956971,695075222,361400724,1835008]"
            sb (10m / 3m) |> equal "3.3333333333333333333333333333 [894784853,-819591186,1807003620,1835008]"
            sb (100m / 3m) |> equal "33.333333333333333333333333333 [894784853,-819591186,1807003620,1769472]"
            sb (1m / 7m) |> equal "0.1428571428571428571428571429 [-1802352347,1376078203,77443012,1835008]"
            sb (-1m / 3m) |> equal "-0.3333333333333333333333333333 [89478485,347537611,180700362,-2145648640]"
            sb (1m / 1.005m) |> equal "0.995024875621890547263681592 [731853880,-1755273125,53940406,1769472]"
            sb (0.0000000000000000000000000001m / 10m) |> equal "0 [0,0,0,0]"
            sb (0.0000000000000000000000000001m / 2m) |> equal "0 [0,0,0,0]"
            sb (0.0000000000000000000000000003m / 2m) |> equal "0.0000000000000000000000000002 [2,0,0,1835008]"
            sb (Decimal.MaxValue / 3m) |> equal "26409387504754779197847983445 [1431655765,1431655765,1431655765,0]"
            sb (1m / 3m * 3m) |> equal "0.9999999999999999999999999999 [268435455,1042612833,542101086,1835008]"
            sb (19.99m / 0.3333333333333333333333333333m) |> equal "59.970000000000000000000000006 [-2113929210,849480301,-1043987082,1769472]"

        testCase "Remainder takes the sign of the dividend and the larger scale" <| fun () ->
            sb (10m % 3m) |> equal "1 [1,0,0,0]"
            sb (-10m % 3m) |> equal "-1 [1,0,0,-2147483648]"
            sb (10m % -3m) |> equal "1 [1,0,0,0]"
            sb (5.5m % 2m) |> equal "1.5 [15,0,0,65536]"
            sb (1m % 0.1m) |> equal "0.0 [0,0,0,65536]"
            sb (-1m % 0.1m) |> equal "0.0 [0,0,0,-2147418112]"
            sb (1m % 3.00m) |> equal "1 [1,0,0,0]"
            sb (0.00m % 3m) |> equal "0.00 [0,0,0,131072]"
            sb (6.0m % 2.000m) |> equal "0.000 [0,0,0,196608]"
            sb (123.456m % 0.9999999999999999999999999999m) |> equal "0.4560000000000000000000000123 [-805306245,1403144387,247198095,1835008]"

        testCase "Remainder is exact for operands of very different scale" <| fun () ->
            sb (10m % 1.0000000000000000000000000001m) |> equal "0.9999999999999999999999999991 [268435447,1042612833,542101086,1835008]"
            sb (Decimal.MaxValue % 0.1m) |> equal "0.0 [0,0,0,65536]"
            sb (Decimal.MaxValue % 1.005m) |> equal "0.885 [885,0,0,196608]"
            sb (Decimal.MaxValue % 19.99m) |> equal "6.15 [615,0,0,131072]"
            sb (-Decimal.MaxValue % 123.456m) |> equal "-123.096 [123096,0,0,-2147287040]"
            sb (1000000000000000000000000000m % 1.005m) |> equal "0.040 [40,0,0,196608]"
            sb (7922816251426433759354395033.5m % 0.3333333333333333333333333333m) |> equal "0.2922816251426433759354395034 [295279002,2055673961,158446186,1835008]"
            sb (18446744073709551616m % 0.9999999999999999999999999999m) |> equal "0.0000000018446744073709551616 [0,0,1,1835008]"

        testCase "Rounding supports every MidpointRounding mode" <| fun () ->
            sb (Math.Round(2.5m, 0, MidpointRounding.ToEven)) |> equal "2 [2,0,0,0]"
            sb (Math.Round(-2.5m, 0, MidpointRounding.ToEven)) |> equal "-2 [2,0,0,-2147483648]"
            sb (Math.Round(2.345m, 2, MidpointRounding.ToEven)) |> equal "2.34 [234,0,0,131072]"
            sb (Math.Round(-2.345m, 2, MidpointRounding.ToEven)) |> equal "-2.34 [234,0,0,-2147352576]"
            sb (Math.Round(-0.049m, 1, MidpointRounding.ToEven)) |> equal "0.0 [0,0,0,-2147418112]"
            sb (Math.Round(1.00m, 1, MidpointRounding.ToEven)) |> equal "1.0 [10,0,0,65536]"
            sb (Math.Round(2.5m, 0, MidpointRounding.AwayFromZero)) |> equal "3 [3,0,0,0]"
            sb (Math.Round(-2.5m, 0, MidpointRounding.AwayFromZero)) |> equal "-3 [3,0,0,-2147483648]"
            sb (Math.Round(2.345m, 2, MidpointRounding.AwayFromZero)) |> equal "2.35 [235,0,0,131072]"
            sb (Math.Round(-2.345m, 2, MidpointRounding.AwayFromZero)) |> equal "-2.35 [235,0,0,-2147352576]"
            sb (Math.Round(-0.049m, 1, MidpointRounding.AwayFromZero)) |> equal "0.0 [0,0,0,-2147418112]"
            sb (Math.Round(1.00m, 1, MidpointRounding.AwayFromZero)) |> equal "1.0 [10,0,0,65536]"
            sb (Math.Round(2.5m, 0, MidpointRounding.ToZero)) |> equal "2 [2,0,0,0]"
            sb (Math.Round(-2.5m, 0, MidpointRounding.ToZero)) |> equal "-2 [2,0,0,-2147483648]"
            sb (Math.Round(2.345m, 2, MidpointRounding.ToZero)) |> equal "2.34 [234,0,0,131072]"
            sb (Math.Round(-2.345m, 2, MidpointRounding.ToZero)) |> equal "-2.34 [234,0,0,-2147352576]"
            sb (Math.Round(-0.049m, 1, MidpointRounding.ToZero)) |> equal "0.0 [0,0,0,-2147418112]"
            sb (Math.Round(1.00m, 1, MidpointRounding.ToZero)) |> equal "1.0 [10,0,0,65536]"
            sb (Math.Round(2.5m, 0, MidpointRounding.ToNegativeInfinity)) |> equal "2 [2,0,0,0]"
            sb (Math.Round(-2.5m, 0, MidpointRounding.ToNegativeInfinity)) |> equal "-3 [3,0,0,-2147483648]"
            sb (Math.Round(2.345m, 2, MidpointRounding.ToNegativeInfinity)) |> equal "2.34 [234,0,0,131072]"
            sb (Math.Round(-2.345m, 2, MidpointRounding.ToNegativeInfinity)) |> equal "-2.35 [235,0,0,-2147352576]"
            sb (Math.Round(-0.049m, 1, MidpointRounding.ToNegativeInfinity)) |> equal "-0.1 [1,0,0,-2147418112]"
            sb (Math.Round(1.00m, 1, MidpointRounding.ToNegativeInfinity)) |> equal "1.0 [10,0,0,65536]"
            sb (Math.Round(2.5m, 0, MidpointRounding.ToPositiveInfinity)) |> equal "3 [3,0,0,0]"
            sb (Math.Round(-2.5m, 0, MidpointRounding.ToPositiveInfinity)) |> equal "-2 [2,0,0,-2147483648]"
            sb (Math.Round(2.345m, 2, MidpointRounding.ToPositiveInfinity)) |> equal "2.35 [235,0,0,131072]"
            sb (Math.Round(-2.345m, 2, MidpointRounding.ToPositiveInfinity)) |> equal "-2.34 [234,0,0,-2147352576]"
            sb (Math.Round(-0.049m, 1, MidpointRounding.ToPositiveInfinity)) |> equal "0.0 [0,0,0,-2147418112]"
            sb (Math.Round(1.00m, 1, MidpointRounding.ToPositiveInfinity)) |> equal "1.0 [10,0,0,65536]"

        testCase "Rounding keeps the scale it does not need to change" <| fun () ->
            sb (Math.Round 2.5m) |> equal "2 [2,0,0,0]"
            sb (Math.Round 3.5m) |> equal "4 [4,0,0,0]"
            sb (Math.Round(1.005m, 2)) |> equal "1.00 [100,0,0,131072]"
            sb (Math.Round(1.5m, 5)) |> equal "1.5 [15,0,0,65536]"
            sb (Math.Round(2.3450m, 28)) |> equal "2.3450 [23450,0,0,262144]"
            sb (Decimal.Round(123.456m, 1)) |> equal "123.5 [1235,0,0,65536]"
            sb (round -0.5m) |> equal "0 [0,0,0,-2147483648]"
            sb (Math.Round(7922816251426433759354395033.5m)) |> equal "7922816251426433759354395034 [-1717986918,-1717986919,429496729,0]"
            sb (Math.Round(0.9999999999999999999999999999m, 3)) |> equal "1.000 [1000,0,0,196608]"
            sb (Math.Round(2.5m, MidpointRounding.AwayFromZero)) |> equal "3 [3,0,0,0]"

        testCase "Truncate, floor and ceiling work and keep the sign of a zero" <| fun () ->
            sb (truncate 2.9m) |> equal "2 [2,0,0,0]"
            sb (truncate -2.9m) |> equal "-2 [2,0,0,-2147483648]"
            sb (truncate -0.5m) |> equal "0 [0,0,0,-2147483648]"
            sb (floor -0.5m) |> equal "-1 [1,0,0,-2147483648]"
            sb (floor 2.5m) |> equal "2 [2,0,0,0]"
            sb (ceil -0.5m) |> equal "0 [0,0,0,-2147483648]"
            sb (ceil 2.1m) |> equal "3 [3,0,0,0]"
            sb (Math.Floor -2.5m) |> equal "-3 [3,0,0,-2147483648]"
            sb (Math.Ceiling 1.00m) |> equal "1 [1,0,0,0]"
            sb (Decimal.Truncate 7922816251426433759354395033.5m) |> equal "7922816251426433759354395033 [-1717986919,-1717986919,429496729,0]"

        testCase "pown multiplies like FSharp.Core" <| fun () ->
            sb (pown 1.1m 3) |> equal "1.331 [1331,0,0,196608]"
            sb (pown 2m 95) |> equal "39614081257132168796771975168 [0,0,-2147483648,0]"
            sb (pown 10m -2) |> equal "0.01 [1,0,0,131072]"
            sb (pown 3m -1) |> equal "0.3333333333333333333333333333 [89478485,347537611,180700362,1835008]"
            sb (pown 1.5m 0) |> equal "1.0 [10,0,0,65536]"
            sb (pown -2.50m 2) |> equal "6.2500 [62500,0,0,262144]"

        testCase "Parsing keeps trailing zeros and rounds past 28 decimals" <| fun () ->
            sb (Decimal.Parse "1.10") |> equal "1.10 [110,0,0,131072]"
            sb (Decimal.Parse "1.100") |> equal "1.100 [1100,0,0,196608]"
            sb (Decimal.Parse " 1.5 ") |> equal "1.5 [15,0,0,65536]"
            sb (Decimal.Parse "1,234.5") |> equal "1234.5 [12345,0,0,65536]"
            sb (Decimal.Parse "-.5") |> equal "-0.5 [5,0,0,-2147418112]"
            sb (Decimal.Parse "-0") |> equal "0 [0,0,0,-2147483648]"
            sb (Decimal.Parse "-0.00") |> equal "0.00 [0,0,0,-2147352576]"
            sb (Decimal.Parse "0001.50") |> equal "1.50 [150,0,0,131072]"
            sb (Decimal.Parse "1.") |> equal "1 [1,0,0,0]"
            sb (Decimal.Parse "0.00000000000000000000000000005") |> equal "0.0000000000000000000000000000 [0,0,0,1835008]"
            sb (Decimal.Parse "0.00000000000000000000000000015") |> equal "0.0000000000000000000000000002 [2,0,0,1835008]"
            sb (Decimal.Parse "0.000000000000000000000000000051") |> equal "0.0000000000000000000000000001 [1,0,0,1835008]"
            sb (Decimal.Parse "79228162514264337593543950335") |> equal "79228162514264337593543950335 [-1,-1,-1,0]"
            sb (Decimal.Parse "79228162514264337593543950335.4") |> equal "79228162514264337593543950335 [-1,-1,-1,0]"
            sb (Decimal.Parse "7922816251426433759354395033.55") |> equal "7922816251426433759354395034 [-1717986918,-1717986919,429496729,0]"
            sb (Decimal.Parse "7922816251426433759354395033.45") |> equal "7922816251426433759354395033.4 [-2,-1,-1,65536]"
            sb (Decimal.Parse "1.23456789012345678901234567895") |> equal "1.2345678901234567890123456790 [1849262358,1186908593,669260594,1835008]"
            sb (Decimal.Parse "1.23456789012345678901234567885") |> equal "1.2345678901234567890123456788 [1849262356,1186908593,669260594,1835008]"
            sb (Decimal.Parse "1.50000000000000000000000000000000") |> equal "1.5000000000000000000000000000 [-1744830464,1563919249,813151629,1835008]"
            sb (Decimal.Parse("1e5", Globalization.NumberStyles.Float)) |> equal "100000 [100000,0,0,0]"
            sb (Decimal.Parse("1E-2", Globalization.NumberStyles.Float)) |> equal "0.01 [1,0,0,131072]"
            sb (decimal "2.50") |> equal "2.50 [250,0,0,131072]"
            sb (Convert.ToDecimal "-7.0") |> equal "-7.0 [70,0,0,-2147418112]"
            (Decimal.TryParse "1.50" |> fun (ok, d) -> ok, sb d) |> equal (true, "1.50 [150,0,0,131072]")
            (Decimal.TryParse "79228162514264337593543950336" |> fst) |> equal false
            (Decimal.TryParse "abc" |> fst) |> equal false

        testCase "ToString round-trips the scale" <| fun () ->
            ((1.10m).ToString()) |> equal "1.10"
            ((-1.10m).ToString()) |> equal "-1.10"
            ((0.00m).ToString()) |> equal "0.00"
            ((100m).ToString()) |> equal "100"
            ((0.0000000000000000000000000001m).ToString()) |> equal "0.0000000000000000000000000001"
            ((-0.5m).ToString()) |> equal "-0.5"
            ((Decimal.MaxValue).ToString()) |> equal "79228162514264337593543950335"
            ((Decimal.MinValue).ToString()) |> equal "-79228162514264337593543950335"
            ((7.9228162514264337593543950335m).ToString()) |> equal "7.9228162514264337593543950335"
            ((1m / 3m).ToString()) |> equal "0.3333333333333333333333333333"
            (string 1.10m) |> equal "1.10"
            ($"{1.10m}") |> equal "1.10"
            (sprintf "%M" 1.10m) |> equal "1.10"
            ((1.105m).ToString("F2")) |> equal "1.11"
            ((1234.5m).ToString("N2")) |> equal "1,234.50"
            (Decimal.Parse((1.2300m).ToString()) |> sb) |> equal "1.2300 [12300,0,0,262144]"
            ((Decimal.Negate 0.0m).ToString()) |> equal "0.0"

        testCase "GetBits exposes the scale and the sign, also of a zero" <| fun () ->
            (Decimal.GetBits(0m)) |> equal [| 0; 0; 0; 0 |]
            (Decimal.GetBits(0.00m)) |> equal [| 0; 0; 0; 131072 |]
            (Decimal.GetBits(Decimal.Negate 0.0m)) |> equal [| 0; 0; 0; -2147418112 |]
            (Decimal.GetBits(-(0.00m))) |> equal [| 0; 0; 0; -2147352576 |]
            (Decimal.GetBits(1.10m)) |> equal [| 110; 0; 0; 131072 |]
            (Decimal.GetBits(-1.10m)) |> equal [| 110; 0; 0; -2147352576 |]
            (Decimal.GetBits(Decimal.MaxValue)) |> equal [| -1; -1; -1; 0 |]
            (Decimal.GetBits(Decimal.MinValue)) |> equal [| -1; -1; -1; -2147483648 |]
            (Decimal.GetBits(0.0000000000000000000000000001m)) |> equal [| 1; 0; 0; 1835008 |]
            (Decimal.GetBits(18446744073709551616m)) |> equal [| 0; 0; 1; 0 |]
            (Decimal.GetBits(0.0m * -1m)) |> equal [| 0; 0; 0; -2147418112 |]
            (Decimal.GetBits(-0.5m + 0.5m)) |> equal [| 0; 0; 0; -2147418112 |]
            (Decimal.GetBits(0.5m - 0.50m)) |> equal [| 0; 0; 0; 131072 |]
            sb (Decimal(1, 0, 0, false, 2uy)) |> equal "0.01 [1,0,0,131072]"
            sb (Decimal(100, 0, 0, true, 2uy)) |> equal "-1.00 [100,0,0,-2147352576]"
            sb (Decimal(-1, -1, -1, false, 0uy)) |> equal "79228162514264337593543950335 [-1,-1,-1,0]"
            sb (Decimal(-1, -1, -1, true, 28uy)) |> equal "-7.9228162514264337593543950335 [-1,-1,-1,-2145648640]"
            sb (Decimal(0, 0, 0, true, 5uy)) |> equal "0.00000 [0,0,0,-2147155968]"
            sb (Decimal([| 1500; 0; 0; 0x20000 |])) |> equal "15.00 [1500,0,0,131072]"
            sb (Decimal(Decimal.GetBits 123.4500m)) |> equal "123.4500 [1234500,0,0,262144]"
            sb (Decimal.Zero) |> equal "0 [0,0,0,0]"
            sb (Decimal.One) |> equal "1 [1,0,0,0]"
            sb (Decimal.MinusOne) |> equal "-1 [1,0,0,-2147483648]"

        testCase "Equality, comparison and hashing ignore trailing zeros" <| fun () ->
            (1.0m = 1.00m) |> equal true
            ((1.0m).Equals(1.00m)) |> equal true
            ((box 1.0m).Equals(box 1.00m)) |> equal true
            (hash 1.0m = hash 1.00m) |> equal true
            ((1.0m).GetHashCode() = (1.00m).GetHashCode()) |> equal true
            (hash 0m = hash 0.00m) |> equal true
            (hash 0m = hash (Decimal.Negate 0.0m)) |> equal true
            (hash 1m <> hash 2m) |> equal true
            (hash 1m <> hash -1m) |> equal true
            (hash 0.1m <> hash 1m) |> equal true
            ((1.0m, 1) = (1.00m, 1)) |> equal true
            (hash (1.0m, 1) = hash (1.00m, 1)) |> equal true
            ([ 1.0m; 2m ] = [ 1.00m; 2.0m ]) |> equal true
            (compare (Some 1.5m) (Some 1.50m)) |> equal 0
            ((1.5m).CompareTo(2m)) |> equal (-1)
            (compare 0.1m 0.10m) |> equal 0
            (compare -1m 0.5m) |> equal (-1)
            (compare Decimal.MaxValue 7922816251426433759354395033.5m) |> equal 1
            (1.5m < 1.50m) |> equal false
            (0.0m = Decimal.Negate 0.00m) |> equal true
            sb (max 1.0m 1.00m) |> equal "1.0 [10,0,0,65536]"
            sb (min 1.0m 1.00m) |> equal "1.00 [100,0,0,131072]"
            sb (Math.Max(1.0m, 2.00m)) |> equal "2.00 [200,0,0,131072]"
            sb (Decimal.Min(-1.0m, 2m)) |> equal "-1.0 [10,0,0,-2147418112]"

        testCase "Decimals work as Set, Map and Dictionary keys" <| fun () ->
            ((set [ 1.0m; 1.00m; 1m; 2m ]).Count) |> equal 2
            (String.Join(";", set [ 2.50m; 1.0m; 1.00m; -3m ] |> Seq.map string)) |> equal "-3;1.0;2.50"
            ((Map.ofList [ 1.0m, "a"; 1.00m, "b" ]).[1m]) |> equal "b"
            (let d = Collections.Generic.Dictionary<decimal, string>() in d.[1.0m] <- "a"; d.[1.00m] <- "b"; d.Count, d.[1m]) |> equal (1, "b")
            ((Collections.Generic.HashSet<decimal>([ 1.0m; 1.00m; 1m; 0.5m; 0.50m ])).Count) |> equal 2
            (String.Join(";", [ 1.0m; 1.00m; 2m ] |> List.distinct |> List.map string)) |> equal "1.0;2"
            (String.Join(";", [ 2.50m; 1.0m; -3m; 1.00m; 0.1m ] |> List.sort |> List.map string)) |> equal "-3;0.1;1.0;1.00;2.50"
            ([ 1.0m; 1.00m; 2m ] |> List.groupBy id |> List.length) |> equal 2

        testCase "Conversions to integers truncate and check the range" <| fun () ->
            (int 2.9m) |> equal 2
            (int -2.9m) |> equal (-2)
            (int64 -9223372036854775808.9m) |> equal (-9223372036854775808L)
            (uint64 18446744073709551615.5m) |> equal 18446744073709551615UL
            (byte 255.9m) |> equal 255uy
            (sbyte -128.5m) |> equal (-128y)
            (uint32 -0.5m) |> equal 0u
            (Convert.ToInt32 2.5m) |> equal 2
            (Convert.ToInt32 3.5m) |> equal 4
            (Convert.ToInt32 -2.5m) |> equal (-2)
            (Decimal.ToInt32 -7.99m) |> equal (-7)
            (char 65m) |> equal 'A'
            (bigint 123.9m) |> equal 123I
            (bigint -79228162514264337593543950335m) |> equal (-79228162514264337593543950335I)

        testCase "Conversions from integers, bigint and strings are exact" <| fun () ->
            sb (decimal 5) |> equal "5 [5,0,0,0]"
            sb (decimal -5) |> equal "-5 [5,0,0,-2147483648]"
            sb (decimal 5L) |> equal "5 [5,0,0,0]"
            sb (decimal Int64.MaxValue) |> equal "9223372036854775807 [-1,2147483647,0,0]"
            sb (decimal Int64.MinValue) |> equal "-9223372036854775808 [0,-2147483648,0,-2147483648]"
            sb (decimal UInt64.MaxValue) |> equal "18446744073709551615 [-1,-1,0,0]"
            sb (decimal 200uy) |> equal "200 [200,0,0,0]"
            sb (decimal 12345678901234567890123456789I) |> equal "12345678901234567890123456789 [1849262357,1186908593,669260594,0]"
            sb (decimal -5I) |> equal "-5 [5,0,0,-2147483648]"
            sb (Decimal(5)) |> equal "5 [5,0,0,0]"
            sb (Decimal(5UL)) |> equal "5 [5,0,0,0]"
            sb (decimal 79228162514264337593543950335I) |> equal "79228162514264337593543950335 [-1,-1,-1,0]"

        testCase "Conversions to and from floats of exactly representable values" <| fun () ->
            (float 1.5m) |> equal 1.5
            (float -0.125m) |> equal (-0.125)
            (float 100m) |> equal 100.0
            (float 1.50m) |> equal 1.5
            (float32 2.5m) |> equal 2.5f
            (Decimal.ToDouble 4294967296.5m) |> equal 4294967296.5
            (float 0m) |> equal 0.0
            sb (decimal 2.5) |> equal "2.5 [25,0,0,65536]"
            sb (decimal -0.125) |> equal "-0.125 [125,0,0,-2147287040]"
            sb (decimal 1e20) |> equal "100000000000000000000 [1661992960,1808227885,5,0]"
            sb (decimal 100.0) |> equal "100 [100,0,0,0]"
            sb (decimal 2.5f) |> equal "2.5 [25,0,0,65536]"
            sb (decimal 0.0) |> equal "0 [0,0,0,0]"
            sb (Decimal(1.5)) |> equal "1.5 [15,0,0,65536]"

        testCase "Overflow raises an OverflowException" <| fun () ->
            thrown (fun () -> Decimal.MaxValue + 1m) |> equal "OverflowException"
            thrown (fun () -> Decimal.MaxValue + 0.5m) |> equal "OverflowException"
            thrown (fun () -> Decimal.MinValue - 1m) |> equal "OverflowException"
            thrown (fun () -> Decimal.MaxValue * 2m) |> equal "OverflowException"
            thrown (fun () -> Decimal.MaxValue / 0.1m) |> equal "OverflowException"
            thrown (fun () -> -Decimal.MaxValue * 1.1m) |> equal "OverflowException"
            thrown (fun () -> pown 2m 96) |> equal "OverflowException"
            thrown (fun () -> Decimal.Parse "79228162514264337593543950336") |> equal "OverflowException"
            thrown (fun () -> Decimal.Parse "79228162514264337593543950335.5") |> equal "OverflowException"
            thrown (fun () -> decimal "100000000000000000000000000000") |> equal "OverflowException"
            thrown (fun () -> decimal 79228162514264337593543950336I) |> equal "OverflowException"
            thrown (fun () -> decimal 8e28) |> equal "OverflowException"
            thrown (fun () -> decimal -8e28) |> equal "OverflowException"
            thrown (fun () -> decimal nan) |> equal "OverflowException"
            thrown (fun () -> decimal infinity) |> equal "OverflowException"
            thrown (fun () -> int 3000000000m) |> equal "OverflowException"
            thrown (fun () -> int64 Decimal.MaxValue) |> equal "OverflowException"
            thrown (fun () -> byte 256m) |> equal "OverflowException"
            thrown (fun () -> byte -1m) |> equal "OverflowException"
            thrown (fun () -> uint64 -1m) |> equal "OverflowException"
            thrown (fun () -> int16 40000m) |> equal "OverflowException"
            thrown (fun () -> sbyte 128m) |> equal "OverflowException"
            thrown (fun () -> char -1m) |> equal "OverflowException"

        testCase "Division and remainder by zero raise a DivideByZeroException" <| fun () ->
            thrown (fun () -> 1m / 0m) |> equal "DivideByZeroException"
            thrown (fun () -> 1m % 0m) |> equal "DivideByZeroException"
            thrown (fun () -> 0m / 0m) |> equal "DivideByZeroException"
            thrown (fun () -> 1m / 0.00m) |> equal "DivideByZeroException"
            thrown (fun () -> Decimal.Divide(1m, 0m)) |> equal "DivideByZeroException"
            thrown (fun () -> Decimal.Remainder(1m, 0m)) |> equal "DivideByZeroException"
            thrown (fun () -> pown 0m -1) |> equal "DivideByZeroException"

        testCase "Invalid arguments raise the .NET exception types" <| fun () ->
            thrown (fun () -> Math.Round(1.5m, 29)) |> equal "ArgumentOutOfRangeException"
            thrown (fun () -> Math.Round(1.5m, -1)) |> equal "ArgumentOutOfRangeException"
            thrown (fun () -> Decimal.Round(1.5m, 29)) |> equal "ArgumentOutOfRangeException"
            thrown (fun () -> Math.Round(1.5m, 29, MidpointRounding.ToZero)) |> equal "ArgumentOutOfRangeException"
            thrown (fun () -> Decimal(1, 0, 0, false, 29uy)) |> equal "ArgumentOutOfRangeException"
            thrown (fun () -> Decimal.Parse "abc") |> equal "FormatException"
            thrown (fun () -> Decimal.Parse "") |> equal "FormatException"
            thrown (fun () -> Decimal.Parse "1e5") |> equal "FormatException"

        testCase "Averages divide by the count as a decimal" <| fun () ->
            sb (List.average [ 1m; 2m; 4m ]) |> equal "2.3333333333333333333333333333 [626349397,-1862204019,1264902534,1835008]"
            sb (List.average [ 1.0m; 2.0m ]) |> equal "1.5 [15,0,0,65536]"
            sb (Seq.average [ 1m; 2m ]) |> equal "1.5 [15,0,0,65536]"
            sb (Array.average [| 0.10m; 0.20m; 0.40m |]) |> equal "0.2333333333333333333333333333 [492131669,1961263246,126490253,1835008]"
            sb (List.averageBy decimal [ 1; 2 ]) |> equal "1.5 [15,0,0,65536]"
            sb (List.sum [ 0.1m; 0.2m; 0.30m ]) |> equal "0.60 [60,0,0,131072]"

        testCase "Scale, IsNegative, IsInteger, Sign, Abs, Compare and CopySign work" <| fun () ->
            ((1.10m).Scale) |> equal 2uy
            ((100m).Scale) |> equal 0uy
            ((0.0000000000000000000000000001m).Scale) |> equal 28uy
            ((1m / 3m).Scale) |> equal 28uy
            (Decimal.IsNegative -1.5m) |> equal true
            (Decimal.IsNegative 0m) |> equal false
            (Decimal.IsNegative (Decimal.Negate 0.0m)) |> equal true
            (Decimal.IsNegative 2m) |> equal false
            (Decimal.IsInteger 2.00m) |> equal true
            (Decimal.IsInteger 2.50m) |> equal false
            (Decimal.IsInteger 0.0m) |> equal true
            (Decimal.IsInteger -3m) |> equal true
            (Decimal.Sign -1.5m) |> equal (-1)
            (Decimal.Sign 0.00m) |> equal 0
            (Decimal.Sign 2m) |> equal 1
            (Decimal.Sign (Decimal.Negate 0.0m)) |> equal 0
            (sign -2.5m) |> equal (-1)
            (Math.Sign 2.5m) |> equal 1
            (Decimal.Compare(2m, 1.5m)) |> equal 1
            (Decimal.Compare(1.0m, 1.00m)) |> equal 0
            (Decimal.Compare(-1m, 1m)) |> equal (-1)
            sb (Decimal.Abs -1.50m) |> equal "1.50 [150,0,0,131072]"
            sb (Decimal.Abs 2m) |> equal "2 [2,0,0,0]"
            sb (Decimal.Abs (Decimal.Negate 0.0m)) |> equal "0.0 [0,0,0,65536]"
            sb (abs -1.50m) |> equal "1.50 [150,0,0,131072]"
            sb (Math.Abs -0.5m) |> equal "0.5 [5,0,0,65536]"
            sb (Decimal.CopySign(1.50m, -2m)) |> equal "-1.50 [150,0,0,-2147352576]"
            sb (Decimal.CopySign(-1.50m, 2m)) |> equal "1.50 [150,0,0,131072]"
            sb (Decimal.CopySign(-1.50m, -2m)) |> equal "-1.50 [150,0,0,-2147352576]"
            sb (Decimal.CopySign(0.0m, -1m)) |> equal "0.0 [0,0,0,-2147418112]"
            sb (Decimal.CopySign(1m, Decimal.Negate 0m)) |> equal "-1 [1,0,0,-2147483648]"
    ]

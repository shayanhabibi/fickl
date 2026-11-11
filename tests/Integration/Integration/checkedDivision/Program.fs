module CheckedDivision

open System

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A, got %A" expected actual

// Not a constant, so the compiler cannot fold the division
let zero = if isNull (Environment.GetEnvironmentVariable "FICKL_NO_SUCH_VARIABLE") then 0 else 1

let divideByZero (f: unit -> 'T) =
    try
        f () |> ignore
        "no exception"
    with
    | :? DivideByZeroException as e -> "DivideByZeroException: " + e.Message
    | e -> "other: " + e.Message

let expected = "DivideByZeroException: Attempted to divide by zero."

divideByZero (fun () -> 10 / zero) |> equal expected
divideByZero (fun () -> 10 % zero) |> equal expected
divideByZero (fun () -> 10uy / byte zero) |> equal expected
divideByZero (fun () -> 10s % int16 zero) |> equal expected
divideByZero (fun () -> 10u / uint32 zero) |> equal expected
divideByZero (fun () -> 10L / int64 zero) |> equal expected
divideByZero (fun () -> 10L % int64 zero) |> equal expected
divideByZero (fun () -> 10UL / uint64 zero) |> equal expected
divideByZero (fun () -> 10I / bigint zero) |> equal expected
divideByZero (fun () -> 10M / decimal zero) |> equal expected
divideByZero (fun () -> 10M % decimal zero) |> equal expected
divideByZero (fun () -> Math.DivRem(10, zero)) |> equal expected
divideByZero (fun () -> Math.DivRem(10L, int64 zero)) |> equal expected

// Floats divide by zero without an exception
(10.0 / float zero) |> equal Double.PositiveInfinity
Double.IsNaN(10.0 % float zero) |> equal true
(LanguagePrimitives.DivideByInt 10.0 zero) |> equal Double.PositiveInfinity

// A divisor other than zero divides as before
let two = zero + 2
(7 / two, 7 % two, -7 / two, -7 % two) |> equal (3, 1, -3, -1)
(7L / int64 two, 7L % int64 two) |> equal (3L, 1L)
(7u / uint32 two, 7uy % byte two) |> equal (3u, 1uy)
(7M / decimal two) |> equal 3.5M
(7I / bigint two) |> equal 3I
Math.DivRem(7, two) |> equal (struct (3, 1))
(10 / 5, 10 % 3) |> equal (2, 1)

printfn "ok"

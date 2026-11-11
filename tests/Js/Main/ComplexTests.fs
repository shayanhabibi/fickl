module Fable.Tests.Complexes

open System
open System.Numerics
open Util.Testing
open Fable.Tests.Util

// System.Numerics.Complex, see fickl #321

let private parts (c: Complex) = c.Real, c.Imaginary

// Math.Sin, Math.Exp... can differ in the last digits between runtimes
let private closeTo (expectedReal: float, expectedImaginary: float) (c: Complex) =
    let close (expected: float) (actual: float) =
        abs (expected - actual) <= 1e-12 * max 1.0 (abs expected)

    if not (close expectedReal c.Real && close expectedImaginary c.Imaginary) then
        failwith $"Expected <{expectedReal}; {expectedImaginary}> but got {c}"

let tests =
  testList "Complex" [
    testCase "Complex constructor and properties work" <| fun () ->
        let c = Complex(3.0, -4.0)
        c.Real |> equal 3.0
        c.Imaginary |> equal -4.0
        c.Magnitude |> equal 5.0
        c.Phase |> equal (Math.Atan2(-4.0, 3.0))
        Complex.Abs c |> equal 5.0

    testCase "Complex multiplication from the issue works" <| fun () ->
        let c = Complex(1.0, 2.0) * Complex(3.0, -1.0)
        sprintf "%f %f" c.Real c.Imaginary |> equal "5.000000 5.000000"

    testCase "Complex constants work" <| fun () ->
        parts Complex.Zero |> equal (0.0, 0.0)
        parts Complex.One |> equal (1.0, 0.0)
        parts Complex.ImaginaryOne |> equal (0.0, 1.0)
        parts (Unchecked.defaultof<Complex>) |> equal (0.0, 0.0)
        Complex.ImaginaryOne * Complex.ImaginaryOne |> equal (Complex(-1.0, 0.0))

    testCase "Complex arithmetic operators work" <| fun () ->
        let a = Complex(1.0, 2.0)
        let b = Complex(3.0, -4.0)
        parts (a + b) |> equal (4.0, -2.0)
        parts (a - b) |> equal (-2.0, 6.0)
        parts (a * b) |> equal (11.0, 2.0)
        parts (a / b) |> equal (-0.2, 0.4)
        parts (b / a) |> equal (-1.0, -2.0)
        parts (-a) |> equal (-1.0, -2.0)

    testCase "Complex arithmetic operators work with floats" <| fun () ->
        let a = Complex(1.0, 2.0)
        parts (a + 2.0) |> equal (3.0, 2.0)
        parts (2.0 + a) |> equal (3.0, 2.0)
        parts (a - 2.0) |> equal (-1.0, 2.0)
        parts (2.0 - a) |> equal (1.0, -2.0)
        parts (a * 2.0) |> equal (2.0, 4.0)
        parts (2.0 * a) |> equal (2.0, 4.0)
        parts (a / 2.0) |> equal (0.5, 1.0)
        parts (2.0 / a) |> equal (0.4, -0.8)

    testCase "Complex static arithmetic methods work" <| fun () ->
        let a = Complex(1.0, 2.0)
        let b = Complex(3.0, -4.0)
        parts (Complex.Add(a, b)) |> equal (4.0, -2.0)
        parts (Complex.Subtract(a, b)) |> equal (-2.0, 6.0)
        parts (Complex.Multiply(a, b)) |> equal (11.0, 2.0)
        parts (Complex.Divide(a, b)) |> equal (-0.2, 0.4)
        parts (Complex.Negate a) |> equal (-1.0, -2.0)
        parts (Complex.Conjugate a) |> equal (1.0, -2.0)
        parts (Complex.Reciprocal a) |> equal (0.2, -0.4)
        parts (Complex.Reciprocal Complex.Zero) |> equal (0.0, 0.0)

    testCase "Complex implicit conversions work" <| fun () ->
        parts (Complex.op_Implicit 3.5) |> equal (3.5, 0.0)
        parts (Complex.op_Implicit 3) |> equal (3.0, 0.0)
        let c: Complex = Complex.Add(Complex.One, 2.5)
        parts c |> equal (3.5, 0.0)

    testCase "Complex.Sqrt works" <| fun () ->
        Complex.Sqrt(Complex(1.0, 2.0)) |> closeTo (1.272019649514069, 0.7861513777574233)
        parts (Complex.Sqrt(Complex(-4.0, 0.0))) |> equal (0.0, 2.0)
        parts (Complex.Sqrt(Complex(9.0, 0.0))) |> equal (3.0, 0.0)
        parts (Complex.Sqrt(Complex(-3.0, -4.0))) |> equal (1.0, -2.0)

    testCase "Complex.Exp, Log and Pow work" <| fun () ->
        let a = Complex(1.0, 2.0)
        Complex.Exp a |> closeTo (-1.1312043837568135, 2.4717266720048188)
        Complex.Log a |> closeTo (0.8047189562170503, 1.1071487177940904)
        Complex.Pow(a, Complex(3.0, -4.0)) |> closeTo (932.1391946432212, 95.9465336603415)
        Complex.Pow(a, 2.0) |> closeTo (-3.0, 4.0)
        parts (Complex.Pow(a, Complex.Zero)) |> equal (1.0, 0.0)
        parts (Complex.Pow(Complex.Zero, a)) |> equal (0.0, 0.0)

    testCase "Complex.Sin and Cos work" <| fun () ->
        let a = Complex(1.0, 2.0)
        Complex.Sin a |> closeTo (3.165778513216168, 1.959601041421606)
        Complex.Cos a |> closeTo (2.0327230070196656, -3.0518977991517997)

    testCase "Complex.FromPolarCoordinates works" <| fun () ->
        Complex.FromPolarCoordinates(2.0, Math.PI / 6.0) |> closeTo (1.7320508075688774, 1.0)
        let c = Complex.FromPolarCoordinates(2.0, 0.5)
        abs (c.Magnitude - 2.0) < 1e-12 |> equal true
        abs (c.Phase - 0.5) < 1e-12 |> equal true

    testCase "Complex equality works" <| fun () ->
        let a = Complex(1.0, 2.0)
        a = Complex(1.0, 2.0) |> equal true
        a = Complex(1.0, 2.5) |> equal false
        a <> Complex(3.0, -4.0) |> equal true
        a.Equals(Complex(1.0, 2.0)) |> equal true
        a.GetHashCode() = Complex(1.0, 2.0).GetHashCode() |> equal true
        // Equals treats NaN as equal to itself, as Double.Equals does
        Complex(nan, 0.0) = Complex(nan, 0.0) |> equal true
        [ a; Complex(1.0, 2.0); Complex.Zero ] |> List.distinct |> List.length |> equal 2

    testCase "Complex.IsNaN, IsInfinity and IsFinite work" <| fun () ->
        Complex.IsNaN(Complex(nan, 0.0)) |> equal true
        Complex.IsNaN(Complex(1.0, 0.0)) |> equal false
        Complex.IsInfinity(Complex(1.0, infinity)) |> equal true
        Complex.IsInfinity(Complex(nan, 0.0)) |> equal false
        Complex.IsFinite(Complex(1.0, 2.0)) |> equal true
        Complex.IsFinite(Complex(1.0, infinity)) |> equal false

    testCase "Complex.ToString works" <| fun () ->
        Complex(1.0, 2.0).ToString() |> equal "<1; 2>"
        string (Complex(3.0, -4.0)) |> equal "<3; -4>"
        Complex(1.5, 0.0).ToString() |> equal "<1.5; 0>"
        sprintf "%O" (Complex(1.0, 2.0)) |> equal "<1; 2>"
        $"{Complex(0.25, -0.5)}" |> equal "<0.25; -0.5>"
  ]

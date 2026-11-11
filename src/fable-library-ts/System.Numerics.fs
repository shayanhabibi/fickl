namespace System.Numerics

open System

module private ComplexHelpers =
    // Math.Sqrt(a * a + b * b) overflows for large components, so scale by the larger one as .NET does
    let hypot (a: float) (b: float) : float =
        let a = Math.Abs a
        let b = Math.Abs b
        let small = Math.Min(a, b)
        let large = Math.Max(a, b)

        if small = 0.0 then
            large
        elif Double.IsPositiveInfinity large && not (Double.IsNaN small) then
            Double.PositiveInfinity
        else
            let ratio = small / large
            large * Math.Sqrt(1.0 + ratio * ratio)

    let isFinite (x: float) : bool =
        not (Double.IsInfinity x) && not (Double.IsNaN x)

    // Above this, the components are scaled down before taking a square root
    let sqrtRescaleThreshold = Double.MaxValue / (1.0 + Math.Sqrt 2.0)

/// System.Numerics.Complex. The algorithms follow .NET, so results agree
/// up to the precision of the underlying Math functions.
[<Sealed; AllowNullLiteral>]
type Complex(real: float, imaginary: float) =
    member _.Real: float = real
    member _.Imaginary: float = imaginary
    member _.Magnitude: float = ComplexHelpers.hypot real imaginary
    member _.Phase: float = Math.Atan2(imaginary, real)

    static member Zero = Complex(0.0, 0.0)
    static member One = Complex(1.0, 0.0)
    static member ImaginaryOne = Complex(0.0, 1.0)
    static member NaN = Complex(Double.NaN, Double.NaN)
    static member Infinity = Complex(Double.PositiveInfinity, Double.PositiveInfinity)

    static member FromPolarCoordinates(magnitude: float, phase: float) =
        Complex(magnitude * Math.Cos phase, magnitude * Math.Sin phase)

    static member op_Implicit(value: float) : Complex = Complex(value, 0.0)
    static member op_Implicit(value: float32) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: int8) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: uint8) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: int16) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: uint16) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: int32) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: uint32) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: int64) : Complex = Complex(float value, 0.0)
    static member op_Implicit(value: uint64) : Complex = Complex(float value, 0.0)

    static member (~-)(value: Complex) = Complex(-value.Real, -value.Imaginary)

    static member (+)(left: Complex, right: Complex) =
        Complex(left.Real + right.Real, left.Imaginary + right.Imaginary)

    static member (+)(left: Complex, right: float) =
        Complex(left.Real + right, left.Imaginary)

    static member (+)(left: float, right: Complex) =
        Complex(left + right.Real, right.Imaginary)

    static member (-)(left: Complex, right: Complex) =
        Complex(left.Real - right.Real, left.Imaginary - right.Imaginary)

    static member (-)(left: Complex, right: float) =
        Complex(left.Real - right, left.Imaginary)

    static member (-)(left: float, right: Complex) =
        Complex(left - right.Real, -right.Imaginary)

    static member (*)(left: Complex, right: Complex) =
        Complex(
            left.Real * right.Real - left.Imaginary * right.Imaginary,
            left.Imaginary * right.Real + left.Real * right.Imaginary
        )

    static member (*)(left: Complex, right: float) =
        Complex(left.Real * right, left.Imaginary * right)

    static member (*)(left: float, right: Complex) =
        Complex(left * right.Real, left * right.Imaginary)

    // Smith formula, which avoids overflow in the intermediate results
    static member (/)(left: Complex, right: Complex) =
        let a = left.Real
        let b = left.Imaginary
        let c = right.Real
        let d = right.Imaginary

        if Math.Abs d < Math.Abs c then
            let doc = d / c
            Complex((a + b * doc) / (c + d * doc), (b - a * doc) / (c + d * doc))
        else
            let cod = c / d
            Complex((b + a * cod) / (d + c * cod), (-a + b * cod) / (d + c * cod))

    static member (/)(left: Complex, right: float) =
        Complex(left.Real / right, left.Imaginary / right)

    static member (/)(left: float, right: Complex) =
        let c = right.Real
        let d = right.Imaginary

        if Math.Abs d < Math.Abs c then
            let doc = d / c
            Complex(left / (c + d * doc), (-left * doc) / (c + d * doc))
        else
            let cod = c / d
            Complex((left * cod) / (d + c * cod), -left / (d + c * cod))

    static member op_Equality(left: Complex, right: Complex) : bool =
        left.Real = right.Real && left.Imaginary = right.Imaginary

    static member op_Inequality(left: Complex, right: Complex) : bool =
        left.Real <> right.Real || left.Imaginary <> right.Imaginary

    static member Negate(value: Complex) : Complex = -value
    static member Add(left: Complex, right: Complex) : Complex = left + right
    static member Add(left: Complex, right: float) : Complex = left + right
    static member Add(left: float, right: Complex) : Complex = left + right
    static member Subtract(left: Complex, right: Complex) : Complex = left - right
    static member Subtract(left: Complex, right: float) : Complex = left - right
    static member Subtract(left: float, right: Complex) : Complex = left - right
    static member Multiply(left: Complex, right: Complex) : Complex = left * right
    static member Multiply(left: Complex, right: float) : Complex = left * right
    static member Multiply(left: float, right: Complex) : Complex = left * right
    static member Divide(dividend: Complex, divisor: Complex) : Complex = dividend / divisor
    static member Divide(dividend: Complex, divisor: float) : Complex = dividend / divisor
    static member Divide(dividend: float, divisor: Complex) : Complex = dividend / divisor

    static member Abs(value: Complex) : float = value.Magnitude

    static member Conjugate(value: Complex) = Complex(value.Real, -value.Imaginary)

    static member Reciprocal(value: Complex) : Complex =
        if value.Real = 0.0 && value.Imaginary = 0.0 then
            Complex.Zero
        else
            Complex.One / value

    static member IsFinite(value: Complex) : bool =
        ComplexHelpers.isFinite value.Real && ComplexHelpers.isFinite value.Imaginary

    static member IsInfinity(value: Complex) : bool =
        Double.IsInfinity value.Real || Double.IsInfinity value.Imaginary

    static member IsNaN(value: Complex) : bool =
        not (Complex.IsInfinity value) && not (Complex.IsFinite value)

    static member Sqrt(value: Complex) : Complex =
        let re = value.Real
        let im = value.Imaginary

        if im = 0.0 then
            if re < 0.0 then
                Complex(0.0, Math.Sqrt(-re))
            else
                Complex(Math.Sqrt re, 0.0)
        else
            let rescale =
                Math.Abs re >= ComplexHelpers.sqrtRescaleThreshold
                || Math.Abs im >= ComplexHelpers.sqrtRescaleThreshold

            if rescale && Double.IsInfinity im && not (Double.IsNaN re) then
                Complex(Double.PositiveInfinity, im)
            else
                let scale =
                    if rescale then
                        0.25
                    else
                        1.0

                let re = re * scale
                let im = im * scale
                let mutable x = 0.0
                let mutable y = 0.0

                if re >= 0.0 then
                    x <- Math.Sqrt((ComplexHelpers.hypot re im + re) * 0.5)
                    y <- im / (2.0 * x)
                else
                    y <- Math.Sqrt((ComplexHelpers.hypot re im - re) * 0.5)

                    if im < 0.0 then
                        y <- -y

                    x <- im / (2.0 * y)

                if rescale then
                    Complex(x * 2.0, y * 2.0)
                else
                    Complex(x, y)

    static member Exp(value: Complex) : Complex =
        let expReal = Math.Exp value.Real
        Complex(expReal * Math.Cos value.Imaginary, expReal * Math.Sin value.Imaginary)

    static member Log(value: Complex) : Complex =
        Complex(Math.Log(Complex.Abs value), Math.Atan2(value.Imaginary, value.Real))

    static member Pow(value: Complex, power: Complex) : Complex =
        if power.Real = 0.0 && power.Imaginary = 0.0 then
            Complex.One
        elif value.Real = 0.0 && value.Imaginary = 0.0 then
            Complex.Zero
        else
            let rho = Complex.Abs value
            let theta = Math.Atan2(value.Imaginary, value.Real)
            let newRho = power.Real * theta + power.Imaginary * Math.Log rho

            let t = Math.Pow(rho, power.Real) * Math.Pow(Math.E, -power.Imaginary * theta)

            Complex(t * Math.Cos newRho, t * Math.Sin newRho)

    static member Pow(value: Complex, power: float) : Complex = Complex.Pow(value, Complex(power, 0.0))

    static member Sin(value: Complex) : Complex =
        let p = Math.Exp value.Imaginary
        let q = 1.0 / p
        let sinh = (p - q) * 0.5
        let cosh = (p + q) * 0.5
        Complex(Math.Sin value.Real * cosh, Math.Cos value.Real * sinh)

    static member Cos(value: Complex) : Complex =
        let p = Math.Exp value.Imaginary
        let q = 1.0 / p
        let sinh = (p - q) * 0.5
        let cosh = (p + q) * 0.5
        Complex(Math.Cos value.Real * cosh, -(Math.Sin value.Real) * sinh)

    override _.Equals(other: obj) =
        match other with
        | :? Complex as other -> real.Equals(other.Real) && imaginary.Equals(other.Imaginary)
        | _ -> false

    override _.GetHashCode() =
        (real.GetHashCode() * 397) ^^^ imaginary.GetHashCode()

    override _.ToString() =
        "<" + real.ToString() + "; " + imaginary.ToString() + ">"

    interface IEquatable<Complex> with
        member this.Equals(other: Complex) = this.Equals(box other)

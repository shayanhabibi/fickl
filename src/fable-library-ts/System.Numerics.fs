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

// System.Numerics.Vector2, Vector3, Vector4 and Matrix4x4. Structs in .NET, classes here: the fields are plain
// properties, so reading `v.X` is a property load and each operation is a small function that returns a new object.
// A float32 is a JavaScript number, so a result can differ from .NET in its last bits until it is stored in a
// Float32Array. Matrix4x4 follows the row-vector convention of .NET: a point is transformed as `v * M`.

[<Sealed; AllowNullLiteral>]
type Vector2 =
    val mutable X: float32
    val mutable Y: float32

    new(x: float32, y: float32)
        =
        {
            X = x
            Y = y
        }

    new(value: float32)
        =
        {
            X = value
            Y = value
        }

    static member Zero = Vector2(0.0f, 0.0f)
    static member One = Vector2(1.0f, 1.0f)
    static member UnitX = Vector2(1.0f, 0.0f)
    static member UnitY = Vector2(0.0f, 1.0f)

    static member (+)(left: Vector2, right: Vector2) =
        Vector2(left.X + right.X, left.Y + right.Y)

    static member (-)(left: Vector2, right: Vector2) =
        Vector2(left.X - right.X, left.Y - right.Y)

    static member (*)(left: Vector2, right: Vector2) =
        Vector2(left.X * right.X, left.Y * right.Y)

    static member (*)(left: Vector2, right: float32) = Vector2(left.X * right, left.Y * right)
    static member (*)(left: float32, right: Vector2) = Vector2(left * right.X, left * right.Y)

    static member (/)(left: Vector2, right: Vector2) =
        Vector2(left.X / right.X, left.Y / right.Y)

    static member (/)(value1: Vector2, value2: float32) =
        Vector2(value1.X / value2, value1.Y / value2)

    static member (~-)(value: Vector2) = Vector2(-value.X, -value.Y)
    static member op_Equality(left: Vector2, right: Vector2) = left.X = right.X && left.Y = right.Y
    static member op_Inequality(left: Vector2, right: Vector2) = left.X <> right.X || left.Y <> right.Y

    static member Add(left: Vector2, right: Vector2) =
        Vector2(left.X + right.X, left.Y + right.Y)

    static member Subtract(left: Vector2, right: Vector2) =
        Vector2(left.X - right.X, left.Y - right.Y)

    static member Multiply(left: Vector2, right: Vector2) =
        Vector2(left.X * right.X, left.Y * right.Y)

    static member Multiply(left: Vector2, right: float32) = Vector2(left.X * right, left.Y * right)
    static member Multiply(left: float32, right: Vector2) = Vector2(left * right.X, left * right.Y)

    static member Divide(left: Vector2, right: Vector2) =
        Vector2(left.X / right.X, left.Y / right.Y)

    static member Divide(left: Vector2, divisor: float32) =
        Vector2(left.X / divisor, left.Y / divisor)

    static member Negate(value: Vector2) = Vector2(-value.X, -value.Y)

    static member Dot(value1: Vector2, value2: Vector2) : float32 =
        value1.X * value2.X + value1.Y * value2.Y

    member this.LengthSquared() : float32 = this.X * this.X + this.Y * this.Y

    member this.Length() : float32 =
        sqrt (this.X * this.X + this.Y * this.Y)

    static member DistanceSquared(value1: Vector2, value2: Vector2) : float32 =
        let dx = value1.X - value2.X
        let dy = value1.Y - value2.Y
        dx * dx + dy * dy

    static member Distance(value1: Vector2, value2: Vector2) : float32 =
        sqrt (Vector2.DistanceSquared(value1, value2))

    static member Normalize(value: Vector2) =
        let length = sqrt (value.X * value.X + value.Y * value.Y)
        Vector2(value.X / length, value.Y / length)

    static member Lerp(value1: Vector2, value2: Vector2, amount: float32) =
        let keep = 1.0f - amount
        Vector2(value1.X * keep + value2.X * amount, value1.Y * keep + value2.Y * amount)

    static member Min(value1: Vector2, value2: Vector2) =
        Vector2(min value1.X value2.X, min value1.Y value2.Y)

    static member Max(value1: Vector2, value2: Vector2) =
        Vector2(max value1.X value2.X, max value1.Y value2.Y)

    static member Abs(value: Vector2) = Vector2(abs value.X, abs value.Y)
    static member SquareRoot(value: Vector2) = Vector2(sqrt value.X, sqrt value.Y)

    static member Clamp(value1: Vector2, min: Vector2, max: Vector2) =
        Vector2.Min(Vector2.Max(value1, min), max)

    static member Reflect(vector: Vector2, normal: Vector2) =
        let twiceDot = 2.0f * (vector.X * normal.X + vector.Y * normal.Y)
        Vector2(vector.X - twiceDot * normal.X, vector.Y - twiceDot * normal.Y)

    static member Transform(position: Vector2, matrix: Matrix4x4) =
        Vector2(
            position.X * matrix.M11 + position.Y * matrix.M21 + matrix.M41,
            position.X * matrix.M12 + position.Y * matrix.M22 + matrix.M42
        )

    static member TransformNormal(normal: Vector2, matrix: Matrix4x4) =
        Vector2(normal.X * matrix.M11 + normal.Y * matrix.M21, normal.X * matrix.M12 + normal.Y * matrix.M22)

    override this.Equals(other: obj) =
        match other with
        | :? Vector2 as other -> this.X = other.X && this.Y = other.Y
        | _ -> false

    override this.GetHashCode() =
        (this.X.GetHashCode() * 397) ^^^ this.Y.GetHashCode()

    override this.ToString() =
        "<" + this.X.ToString() + ", " + this.Y.ToString() + ">"

    interface IEquatable<Vector2> with
        member this.Equals(other: Vector2) = this.Equals(box other)

and [<Sealed; AllowNullLiteral>] Vector3 =
    val mutable X: float32
    val mutable Y: float32
    val mutable Z: float32

    new(x: float32, y: float32, z: float32)
        =
        {
            X = x
            Y = y
            Z = z
        }

    new(value: float32)
        =
        {
            X = value
            Y = value
            Z = value
        }

    new(value: Vector2, z: float32)
        =
        {
            X = value.X
            Y = value.Y
            Z = z
        }

    static member Zero = Vector3(0.0f, 0.0f, 0.0f)
    static member One = Vector3(1.0f, 1.0f, 1.0f)
    static member UnitX = Vector3(1.0f, 0.0f, 0.0f)
    static member UnitY = Vector3(0.0f, 1.0f, 0.0f)
    static member UnitZ = Vector3(0.0f, 0.0f, 1.0f)

    static member (+)(left: Vector3, right: Vector3) =
        Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z)

    static member (-)(left: Vector3, right: Vector3) =
        Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z)

    static member (*)(left: Vector3, right: Vector3) =
        Vector3(left.X * right.X, left.Y * right.Y, left.Z * right.Z)

    static member (*)(left: Vector3, right: float32) =
        Vector3(left.X * right, left.Y * right, left.Z * right)

    static member (*)(left: float32, right: Vector3) =
        Vector3(left * right.X, left * right.Y, left * right.Z)

    static member (/)(left: Vector3, right: Vector3) =
        Vector3(left.X / right.X, left.Y / right.Y, left.Z / right.Z)

    static member (/)(value1: Vector3, value2: float32) =
        Vector3(value1.X / value2, value1.Y / value2, value1.Z / value2)

    static member (~-)(value: Vector3) = Vector3(-value.X, -value.Y, -value.Z)

    static member op_Equality(left: Vector3, right: Vector3) =
        left.X = right.X && left.Y = right.Y && left.Z = right.Z

    static member op_Inequality(left: Vector3, right: Vector3) =
        left.X <> right.X || left.Y <> right.Y || left.Z <> right.Z

    static member Add(left: Vector3, right: Vector3) =
        Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z)

    static member Subtract(left: Vector3, right: Vector3) =
        Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z)

    static member Multiply(left: Vector3, right: Vector3) =
        Vector3(left.X * right.X, left.Y * right.Y, left.Z * right.Z)

    static member Multiply(left: Vector3, right: float32) =
        Vector3(left.X * right, left.Y * right, left.Z * right)

    static member Multiply(left: float32, right: Vector3) =
        Vector3(left * right.X, left * right.Y, left * right.Z)

    static member Divide(left: Vector3, right: Vector3) =
        Vector3(left.X / right.X, left.Y / right.Y, left.Z / right.Z)

    static member Divide(left: Vector3, divisor: float32) =
        Vector3(left.X / divisor, left.Y / divisor, left.Z / divisor)

    static member Negate(value: Vector3) = Vector3(-value.X, -value.Y, -value.Z)

    static member Dot(vector1: Vector3, vector2: Vector3) : float32 =
        vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z

    static member Cross(vector1: Vector3, vector2: Vector3) =
        Vector3(
            vector1.Y * vector2.Z - vector1.Z * vector2.Y,
            vector1.Z * vector2.X - vector1.X * vector2.Z,
            vector1.X * vector2.Y - vector1.Y * vector2.X
        )

    member this.LengthSquared() : float32 =
        this.X * this.X + this.Y * this.Y + this.Z * this.Z

    member this.Length() : float32 =
        sqrt (this.X * this.X + this.Y * this.Y + this.Z * this.Z)

    static member DistanceSquared(value1: Vector3, value2: Vector3) : float32 =
        let dx = value1.X - value2.X
        let dy = value1.Y - value2.Y
        let dz = value1.Z - value2.Z
        dx * dx + dy * dy + dz * dz

    static member Distance(value1: Vector3, value2: Vector3) : float32 =
        sqrt (Vector3.DistanceSquared(value1, value2))

    static member Normalize(value: Vector3) =
        let length = sqrt (value.X * value.X + value.Y * value.Y + value.Z * value.Z)
        Vector3(value.X / length, value.Y / length, value.Z / length)

    static member Lerp(value1: Vector3, value2: Vector3, amount: float32) =
        let keep = 1.0f - amount

        Vector3(
            value1.X * keep + value2.X * amount,
            value1.Y * keep + value2.Y * amount,
            value1.Z * keep + value2.Z * amount
        )

    static member Min(value1: Vector3, value2: Vector3) =
        Vector3(min value1.X value2.X, min value1.Y value2.Y, min value1.Z value2.Z)

    static member Max(value1: Vector3, value2: Vector3) =
        Vector3(max value1.X value2.X, max value1.Y value2.Y, max value1.Z value2.Z)

    static member Abs(value: Vector3) =
        Vector3(abs value.X, abs value.Y, abs value.Z)

    static member SquareRoot(value: Vector3) =
        Vector3(sqrt value.X, sqrt value.Y, sqrt value.Z)

    static member Clamp(value1: Vector3, min: Vector3, max: Vector3) =
        Vector3.Min(Vector3.Max(value1, min), max)

    static member Reflect(vector: Vector3, normal: Vector3) =
        let twiceDot =
            2.0f * (vector.X * normal.X + vector.Y * normal.Y + vector.Z * normal.Z)

        Vector3(vector.X - twiceDot * normal.X, vector.Y - twiceDot * normal.Y, vector.Z - twiceDot * normal.Z)

    static member Transform(position: Vector3, matrix: Matrix4x4) =
        Vector3(
            position.X * matrix.M11
            + position.Y * matrix.M21
            + position.Z * matrix.M31
            + matrix.M41,
            position.X * matrix.M12
            + position.Y * matrix.M22
            + position.Z * matrix.M32
            + matrix.M42,
            position.X * matrix.M13
            + position.Y * matrix.M23
            + position.Z * matrix.M33
            + matrix.M43
        )

    static member TransformNormal(normal: Vector3, matrix: Matrix4x4) =
        Vector3(
            normal.X * matrix.M11 + normal.Y * matrix.M21 + normal.Z * matrix.M31,
            normal.X * matrix.M12 + normal.Y * matrix.M22 + normal.Z * matrix.M32,
            normal.X * matrix.M13 + normal.Y * matrix.M23 + normal.Z * matrix.M33
        )

    override this.Equals(other: obj) =
        match other with
        | :? Vector3 as other -> this.X = other.X && this.Y = other.Y && this.Z = other.Z
        | _ -> false

    override this.GetHashCode() =
        (((this.X.GetHashCode() * 397) ^^^ this.Y.GetHashCode()) * 397)
        ^^^ this.Z.GetHashCode()

    override this.ToString() =
        "<"
        + this.X.ToString()
        + ", "
        + this.Y.ToString()
        + ", "
        + this.Z.ToString()
        + ">"

    interface IEquatable<Vector3> with
        member this.Equals(other: Vector3) = this.Equals(box other)

and [<Sealed; AllowNullLiteral>] Vector4 =
    val mutable X: float32
    val mutable Y: float32
    val mutable Z: float32
    val mutable W: float32

    new(x: float32, y: float32, z: float32, w: float32)
        =
        {
            X = x
            Y = y
            Z = z
            W = w
        }

    new(value: float32)
        =
        {
            X = value
            Y = value
            Z = value
            W = value
        }

    new(value: Vector2, z: float32, w: float32)
        =
        {
            X = value.X
            Y = value.Y
            Z = z
            W = w
        }

    new(value: Vector3, w: float32)
        =
        {
            X = value.X
            Y = value.Y
            Z = value.Z
            W = w
        }

    static member Zero = Vector4(0.0f, 0.0f, 0.0f, 0.0f)
    static member One = Vector4(1.0f, 1.0f, 1.0f, 1.0f)
    static member UnitX = Vector4(1.0f, 0.0f, 0.0f, 0.0f)
    static member UnitY = Vector4(0.0f, 1.0f, 0.0f, 0.0f)
    static member UnitZ = Vector4(0.0f, 0.0f, 1.0f, 0.0f)
    static member UnitW = Vector4(0.0f, 0.0f, 0.0f, 1.0f)

    static member (+)(left: Vector4, right: Vector4) =
        Vector4(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W)

    static member (-)(left: Vector4, right: Vector4) =
        Vector4(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W)

    static member (*)(left: Vector4, right: Vector4) =
        Vector4(left.X * right.X, left.Y * right.Y, left.Z * right.Z, left.W * right.W)

    static member (*)(left: Vector4, right: float32) =
        Vector4(left.X * right, left.Y * right, left.Z * right, left.W * right)

    static member (*)(left: float32, right: Vector4) =
        Vector4(left * right.X, left * right.Y, left * right.Z, left * right.W)

    static member (/)(left: Vector4, right: Vector4) =
        Vector4(left.X / right.X, left.Y / right.Y, left.Z / right.Z, left.W / right.W)

    static member (/)(value1: Vector4, value2: float32) =
        Vector4(value1.X / value2, value1.Y / value2, value1.Z / value2, value1.W / value2)

    static member (~-)(value: Vector4) =
        Vector4(-value.X, -value.Y, -value.Z, -value.W)

    static member op_Equality(left: Vector4, right: Vector4) =
        left.X = right.X && left.Y = right.Y && left.Z = right.Z && left.W = right.W

    static member op_Inequality(left: Vector4, right: Vector4) =
        left.X <> right.X || left.Y <> right.Y || left.Z <> right.Z || left.W <> right.W

    static member Add(left: Vector4, right: Vector4) =
        Vector4(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W)

    static member Subtract(left: Vector4, right: Vector4) =
        Vector4(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W)

    static member Multiply(left: Vector4, right: Vector4) =
        Vector4(left.X * right.X, left.Y * right.Y, left.Z * right.Z, left.W * right.W)

    static member Multiply(left: Vector4, right: float32) =
        Vector4(left.X * right, left.Y * right, left.Z * right, left.W * right)

    static member Multiply(left: float32, right: Vector4) =
        Vector4(left * right.X, left * right.Y, left * right.Z, left * right.W)

    static member Divide(left: Vector4, right: Vector4) =
        Vector4(left.X / right.X, left.Y / right.Y, left.Z / right.Z, left.W / right.W)

    static member Divide(left: Vector4, divisor: float32) =
        Vector4(left.X / divisor, left.Y / divisor, left.Z / divisor, left.W / divisor)

    static member Negate(value: Vector4) =
        Vector4(-value.X, -value.Y, -value.Z, -value.W)

    static member Dot(vector1: Vector4, vector2: Vector4) : float32 =
        vector1.X * vector2.X
        + vector1.Y * vector2.Y
        + vector1.Z * vector2.Z
        + vector1.W * vector2.W

    member this.LengthSquared() : float32 =
        this.X * this.X + this.Y * this.Y + this.Z * this.Z + this.W * this.W

    member this.Length() : float32 =
        sqrt (this.X * this.X + this.Y * this.Y + this.Z * this.Z + this.W * this.W)

    static member DistanceSquared(value1: Vector4, value2: Vector4) : float32 =
        let dx = value1.X - value2.X
        let dy = value1.Y - value2.Y
        let dz = value1.Z - value2.Z
        let dw = value1.W - value2.W
        dx * dx + dy * dy + dz * dz + dw * dw

    static member Distance(value1: Vector4, value2: Vector4) : float32 =
        sqrt (Vector4.DistanceSquared(value1, value2))

    static member Normalize(vector: Vector4) =
        let length =
            sqrt (
                vector.X * vector.X
                + vector.Y * vector.Y
                + vector.Z * vector.Z
                + vector.W * vector.W
            )

        Vector4(vector.X / length, vector.Y / length, vector.Z / length, vector.W / length)

    static member Lerp(value1: Vector4, value2: Vector4, amount: float32) =
        let keep = 1.0f - amount

        Vector4(
            value1.X * keep + value2.X * amount,
            value1.Y * keep + value2.Y * amount,
            value1.Z * keep + value2.Z * amount,
            value1.W * keep + value2.W * amount
        )

    static member Min(value1: Vector4, value2: Vector4) =
        Vector4(min value1.X value2.X, min value1.Y value2.Y, min value1.Z value2.Z, min value1.W value2.W)

    static member Max(value1: Vector4, value2: Vector4) =
        Vector4(max value1.X value2.X, max value1.Y value2.Y, max value1.Z value2.Z, max value1.W value2.W)

    static member Abs(value: Vector4) =
        Vector4(abs value.X, abs value.Y, abs value.Z, abs value.W)

    static member SquareRoot(value: Vector4) =
        Vector4(sqrt value.X, sqrt value.Y, sqrt value.Z, sqrt value.W)

    static member Clamp(value1: Vector4, min: Vector4, max: Vector4) =
        Vector4.Min(Vector4.Max(value1, min), max)

    static member Transform(position: Vector2, matrix: Matrix4x4) =
        Vector4(
            position.X * matrix.M11 + position.Y * matrix.M21 + matrix.M41,
            position.X * matrix.M12 + position.Y * matrix.M22 + matrix.M42,
            position.X * matrix.M13 + position.Y * matrix.M23 + matrix.M43,
            position.X * matrix.M14 + position.Y * matrix.M24 + matrix.M44
        )

    static member Transform(position: Vector3, matrix: Matrix4x4) =
        Vector4(
            position.X * matrix.M11
            + position.Y * matrix.M21
            + position.Z * matrix.M31
            + matrix.M41,
            position.X * matrix.M12
            + position.Y * matrix.M22
            + position.Z * matrix.M32
            + matrix.M42,
            position.X * matrix.M13
            + position.Y * matrix.M23
            + position.Z * matrix.M33
            + matrix.M43,
            position.X * matrix.M14
            + position.Y * matrix.M24
            + position.Z * matrix.M34
            + matrix.M44
        )

    static member Transform(vector: Vector4, matrix: Matrix4x4) =
        Vector4(
            vector.X * matrix.M11
            + vector.Y * matrix.M21
            + vector.Z * matrix.M31
            + vector.W * matrix.M41,
            vector.X * matrix.M12
            + vector.Y * matrix.M22
            + vector.Z * matrix.M32
            + vector.W * matrix.M42,
            vector.X * matrix.M13
            + vector.Y * matrix.M23
            + vector.Z * matrix.M33
            + vector.W * matrix.M43,
            vector.X * matrix.M14
            + vector.Y * matrix.M24
            + vector.Z * matrix.M34
            + vector.W * matrix.M44
        )

    override this.Equals(other: obj) =
        match other with
        | :? Vector4 as other -> this.X = other.X && this.Y = other.Y && this.Z = other.Z && this.W = other.W
        | _ -> false

    override this.GetHashCode() =
        (((((this.X.GetHashCode() * 397) ^^^ this.Y.GetHashCode()) * 397)
          ^^^ this.Z.GetHashCode())
         * 397)
        ^^^ this.W.GetHashCode()

    override this.ToString() =
        "<"
        + this.X.ToString()
        + ", "
        + this.Y.ToString()
        + ", "
        + this.Z.ToString()
        + ", "
        + this.W.ToString()
        + ">"

    interface IEquatable<Vector4> with
        member this.Equals(other: Vector4) = this.Equals(box other)

and [<Sealed; AllowNullLiteral>] Matrix4x4 =
    val mutable M11: float32
    val mutable M12: float32
    val mutable M13: float32
    val mutable M14: float32
    val mutable M21: float32
    val mutable M22: float32
    val mutable M23: float32
    val mutable M24: float32
    val mutable M31: float32
    val mutable M32: float32
    val mutable M33: float32
    val mutable M34: float32
    val mutable M41: float32
    val mutable M42: float32
    val mutable M43: float32
    val mutable M44: float32

    new
        (
            m11: float32,
            m12: float32,
            m13: float32,
            m14: float32,
            m21: float32,
            m22: float32,
            m23: float32,
            m24: float32,
            m31: float32,
            m32: float32,
            m33: float32,
            m34: float32,
            m41: float32,
            m42: float32,
            m43: float32,
            m44: float32
        )
        =
        {
            M11 = m11
            M12 = m12
            M13 = m13
            M14 = m14
            M21 = m21
            M22 = m22
            M23 = m23
            M24 = m24
            M31 = m31
            M32 = m32
            M33 = m33
            M34 = m34
            M41 = m41
            M42 = m42
            M43 = m43
            M44 = m44
        }

    /// The default value of the struct in .NET. Not a member of the .NET type.
    static member Zero =
        Matrix4x4(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f)

    static member Identity =
        Matrix4x4(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f)

    member this.IsIdentity: bool =
        this.M11 = 1.0f
        && this.M22 = 1.0f
        && this.M33 = 1.0f
        && this.M44 = 1.0f
        && this.M12 = 0.0f
        && this.M13 = 0.0f
        && this.M14 = 0.0f
        && this.M21 = 0.0f
        && this.M23 = 0.0f
        && this.M24 = 0.0f
        && this.M31 = 0.0f
        && this.M32 = 0.0f
        && this.M34 = 0.0f
        && this.M41 = 0.0f
        && this.M42 = 0.0f
        && this.M43 = 0.0f

    member this.Translation
        with get () = Vector3(this.M41, this.M42, this.M43)
        and set (value: Vector3) =
            this.M41 <- value.X
            this.M42 <- value.Y
            this.M43 <- value.Z

    static member (+)(value1: Matrix4x4, value2: Matrix4x4) =
        Matrix4x4(
            value1.M11 + value2.M11,
            value1.M12 + value2.M12,
            value1.M13 + value2.M13,
            value1.M14 + value2.M14,
            value1.M21 + value2.M21,
            value1.M22 + value2.M22,
            value1.M23 + value2.M23,
            value1.M24 + value2.M24,
            value1.M31 + value2.M31,
            value1.M32 + value2.M32,
            value1.M33 + value2.M33,
            value1.M34 + value2.M34,
            value1.M41 + value2.M41,
            value1.M42 + value2.M42,
            value1.M43 + value2.M43,
            value1.M44 + value2.M44
        )

    static member (-)(value1: Matrix4x4, value2: Matrix4x4) =
        Matrix4x4(
            value1.M11 - value2.M11,
            value1.M12 - value2.M12,
            value1.M13 - value2.M13,
            value1.M14 - value2.M14,
            value1.M21 - value2.M21,
            value1.M22 - value2.M22,
            value1.M23 - value2.M23,
            value1.M24 - value2.M24,
            value1.M31 - value2.M31,
            value1.M32 - value2.M32,
            value1.M33 - value2.M33,
            value1.M34 - value2.M34,
            value1.M41 - value2.M41,
            value1.M42 - value2.M42,
            value1.M43 - value2.M43,
            value1.M44 - value2.M44
        )

    static member (~-)(value: Matrix4x4) =
        Matrix4x4(
            -value.M11,
            -value.M12,
            -value.M13,
            -value.M14,
            -value.M21,
            -value.M22,
            -value.M23,
            -value.M24,
            -value.M31,
            -value.M32,
            -value.M33,
            -value.M34,
            -value.M41,
            -value.M42,
            -value.M43,
            -value.M44
        )

    static member (*)(value1: Matrix4x4, value2: Matrix4x4) =
        Matrix4x4(
            value1.M11 * value2.M11
            + value1.M12 * value2.M21
            + value1.M13 * value2.M31
            + value1.M14 * value2.M41,
            value1.M11 * value2.M12
            + value1.M12 * value2.M22
            + value1.M13 * value2.M32
            + value1.M14 * value2.M42,
            value1.M11 * value2.M13
            + value1.M12 * value2.M23
            + value1.M13 * value2.M33
            + value1.M14 * value2.M43,
            value1.M11 * value2.M14
            + value1.M12 * value2.M24
            + value1.M13 * value2.M34
            + value1.M14 * value2.M44,
            value1.M21 * value2.M11
            + value1.M22 * value2.M21
            + value1.M23 * value2.M31
            + value1.M24 * value2.M41,
            value1.M21 * value2.M12
            + value1.M22 * value2.M22
            + value1.M23 * value2.M32
            + value1.M24 * value2.M42,
            value1.M21 * value2.M13
            + value1.M22 * value2.M23
            + value1.M23 * value2.M33
            + value1.M24 * value2.M43,
            value1.M21 * value2.M14
            + value1.M22 * value2.M24
            + value1.M23 * value2.M34
            + value1.M24 * value2.M44,
            value1.M31 * value2.M11
            + value1.M32 * value2.M21
            + value1.M33 * value2.M31
            + value1.M34 * value2.M41,
            value1.M31 * value2.M12
            + value1.M32 * value2.M22
            + value1.M33 * value2.M32
            + value1.M34 * value2.M42,
            value1.M31 * value2.M13
            + value1.M32 * value2.M23
            + value1.M33 * value2.M33
            + value1.M34 * value2.M43,
            value1.M31 * value2.M14
            + value1.M32 * value2.M24
            + value1.M33 * value2.M34
            + value1.M34 * value2.M44,
            value1.M41 * value2.M11
            + value1.M42 * value2.M21
            + value1.M43 * value2.M31
            + value1.M44 * value2.M41,
            value1.M41 * value2.M12
            + value1.M42 * value2.M22
            + value1.M43 * value2.M32
            + value1.M44 * value2.M42,
            value1.M41 * value2.M13
            + value1.M42 * value2.M23
            + value1.M43 * value2.M33
            + value1.M44 * value2.M43,
            value1.M41 * value2.M14
            + value1.M42 * value2.M24
            + value1.M43 * value2.M34
            + value1.M44 * value2.M44
        )

    static member (*)(value1: Matrix4x4, value2: float32) =
        Matrix4x4(
            value1.M11 * value2,
            value1.M12 * value2,
            value1.M13 * value2,
            value1.M14 * value2,
            value1.M21 * value2,
            value1.M22 * value2,
            value1.M23 * value2,
            value1.M24 * value2,
            value1.M31 * value2,
            value1.M32 * value2,
            value1.M33 * value2,
            value1.M34 * value2,
            value1.M41 * value2,
            value1.M42 * value2,
            value1.M43 * value2,
            value1.M44 * value2
        )

    static member op_Equality(value1: Matrix4x4, value2: Matrix4x4) = value1.Equals(value2)
    static member op_Inequality(value1: Matrix4x4, value2: Matrix4x4) = not (value1.Equals(value2))

    static member Add(value1: Matrix4x4, value2: Matrix4x4) = value1 + value2
    static member Subtract(value1: Matrix4x4, value2: Matrix4x4) = value1 - value2
    static member Multiply(value1: Matrix4x4, value2: Matrix4x4) = value1 * value2
    static member Multiply(value1: Matrix4x4, value2: float32) = value1 * value2
    static member Negate(value: Matrix4x4) = -value

    static member Lerp(matrix1: Matrix4x4, matrix2: Matrix4x4, amount: float32) =
        Matrix4x4(
            matrix1.M11 + (matrix2.M11 - matrix1.M11) * amount,
            matrix1.M12 + (matrix2.M12 - matrix1.M12) * amount,
            matrix1.M13 + (matrix2.M13 - matrix1.M13) * amount,
            matrix1.M14 + (matrix2.M14 - matrix1.M14) * amount,
            matrix1.M21 + (matrix2.M21 - matrix1.M21) * amount,
            matrix1.M22 + (matrix2.M22 - matrix1.M22) * amount,
            matrix1.M23 + (matrix2.M23 - matrix1.M23) * amount,
            matrix1.M24 + (matrix2.M24 - matrix1.M24) * amount,
            matrix1.M31 + (matrix2.M31 - matrix1.M31) * amount,
            matrix1.M32 + (matrix2.M32 - matrix1.M32) * amount,
            matrix1.M33 + (matrix2.M33 - matrix1.M33) * amount,
            matrix1.M34 + (matrix2.M34 - matrix1.M34) * amount,
            matrix1.M41 + (matrix2.M41 - matrix1.M41) * amount,
            matrix1.M42 + (matrix2.M42 - matrix1.M42) * amount,
            matrix1.M43 + (matrix2.M43 - matrix1.M43) * amount,
            matrix1.M44 + (matrix2.M44 - matrix1.M44) * amount
        )

    static member Transpose(matrix: Matrix4x4) =
        Matrix4x4(
            matrix.M11,
            matrix.M21,
            matrix.M31,
            matrix.M41,
            matrix.M12,
            matrix.M22,
            matrix.M32,
            matrix.M42,
            matrix.M13,
            matrix.M23,
            matrix.M33,
            matrix.M43,
            matrix.M14,
            matrix.M24,
            matrix.M34,
            matrix.M44
        )

    static member CreateTranslation(xPosition: float32, yPosition: float32, zPosition: float32) =
        Matrix4x4(
            1.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            1.0f,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            1.0f,
            0.0f,
            xPosition,
            yPosition,
            zPosition,
            1.0f
        )

    static member CreateTranslation(position: Vector3) =
        Matrix4x4.CreateTranslation(position.X, position.Y, position.Z)

    static member CreateScale(xScale: float32, yScale: float32, zScale: float32) =
        Matrix4x4(xScale, 0.0f, 0.0f, 0.0f, 0.0f, yScale, 0.0f, 0.0f, 0.0f, 0.0f, zScale, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f)

    static member CreateScale(scales: Vector3) =
        Matrix4x4.CreateScale(scales.X, scales.Y, scales.Z)

    static member CreateScale(scale: float32) =
        Matrix4x4.CreateScale(scale, scale, scale)

    static member CreateScale(xScale: float32, yScale: float32, zScale: float32, centerPoint: Vector3) =
        Matrix4x4(
            xScale,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            yScale,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            zScale,
            0.0f,
            centerPoint.X * (1.0f - xScale),
            centerPoint.Y * (1.0f - yScale),
            centerPoint.Z * (1.0f - zScale),
            1.0f
        )

    static member CreateScale(scales: Vector3, centerPoint: Vector3) =
        Matrix4x4.CreateScale(scales.X, scales.Y, scales.Z, centerPoint)

    static member CreateScale(scale: float32, centerPoint: Vector3) =
        Matrix4x4.CreateScale(scale, scale, scale, centerPoint)

    static member CreateRotationX(radians: float32) =
        let c = cos radians
        let s = sin radians
        Matrix4x4(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, c, s, 0.0f, 0.0f, -s, c, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f)

    static member CreateRotationY(radians: float32) =
        let c = cos radians
        let s = sin radians
        Matrix4x4(c, 0.0f, -s, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, s, 0.0f, c, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f)

    static member CreateRotationZ(radians: float32) =
        let c = cos radians
        let s = sin radians
        Matrix4x4(c, s, 0.0f, 0.0f, -s, c, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f)

    static member CreateFromAxisAngle(axis: Vector3, angle: float32) =
        let x = axis.X
        let y = axis.Y
        let z = axis.Z
        let sa = sin angle
        let ca = cos angle
        let xx = x * x
        let yy = y * y
        let zz = z * z
        let xy = x * y
        let xz = x * z
        let yz = y * z

        Matrix4x4(
            xx + ca * (1.0f - xx),
            xy - ca * xy + sa * z,
            xz - ca * xz - sa * y,
            0.0f,
            xy - ca * xy - sa * z,
            yy + ca * (1.0f - yy),
            yz - ca * yz + sa * x,
            0.0f,
            xz - ca * xz + sa * y,
            yz - ca * yz - sa * x,
            zz + ca * (1.0f - zz),
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            1.0f
        )

    static member CreateLookAt(cameraPosition: Vector3, cameraTarget: Vector3, cameraUpVector: Vector3) =
        let zAxis = Vector3.Normalize(cameraPosition - cameraTarget)
        let xAxis = Vector3.Normalize(Vector3.Cross(cameraUpVector, zAxis))
        let yAxis = Vector3.Cross(zAxis, xAxis)

        Matrix4x4(
            xAxis.X,
            yAxis.X,
            zAxis.X,
            0.0f,
            xAxis.Y,
            yAxis.Y,
            zAxis.Y,
            0.0f,
            xAxis.Z,
            yAxis.Z,
            zAxis.Z,
            0.0f,
            -Vector3.Dot(xAxis, cameraPosition),
            -Vector3.Dot(yAxis, cameraPosition),
            -Vector3.Dot(zAxis, cameraPosition),
            1.0f
        )

    static member CreatePerspectiveFieldOfView
        (fieldOfView: float32, aspectRatio: float32, nearPlaneDistance: float32, farPlaneDistance: float32)
        =
        if fieldOfView <= 0.0f || fieldOfView >= float32 Math.PI then
            raise (ArgumentOutOfRangeException "fieldOfView")

        if nearPlaneDistance <= 0.0f then
            raise (ArgumentOutOfRangeException "nearPlaneDistance")

        if farPlaneDistance <= 0.0f then
            raise (ArgumentOutOfRangeException "farPlaneDistance")

        if nearPlaneDistance >= farPlaneDistance then
            raise (ArgumentOutOfRangeException "nearPlaneDistance")

        let yScale = 1.0f / tan (fieldOfView * 0.5f)
        let xScale = yScale / aspectRatio

        let negFarRange =
            if Single.IsPositiveInfinity farPlaneDistance then
                -1.0f
            else
                farPlaneDistance / (nearPlaneDistance - farPlaneDistance)

        Matrix4x4(
            xScale,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            yScale,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            negFarRange,
            -1.0f,
            0.0f,
            0.0f,
            nearPlaneDistance * negFarRange,
            0.0f
        )

    static member CreatePerspective
        (width: float32, height: float32, nearPlaneDistance: float32, farPlaneDistance: float32)
        =
        if nearPlaneDistance <= 0.0f then
            raise (ArgumentOutOfRangeException "nearPlaneDistance")

        if farPlaneDistance <= 0.0f then
            raise (ArgumentOutOfRangeException "farPlaneDistance")

        if nearPlaneDistance >= farPlaneDistance then
            raise (ArgumentOutOfRangeException "nearPlaneDistance")

        let negFarRange =
            if Single.IsPositiveInfinity farPlaneDistance then
                -1.0f
            else
                farPlaneDistance / (nearPlaneDistance - farPlaneDistance)

        Matrix4x4(
            2.0f * nearPlaneDistance / width,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            2.0f * nearPlaneDistance / height,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            negFarRange,
            -1.0f,
            0.0f,
            0.0f,
            nearPlaneDistance * negFarRange,
            0.0f
        )

    static member CreateOrthographic(width: float32, height: float32, zNearPlane: float32, zFarPlane: float32) =
        let range = 1.0f / (zNearPlane - zFarPlane)

        Matrix4x4(
            2.0f / width,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            2.0f / height,
            0.0f,
            0.0f,
            0.0f,
            0.0f,
            range,
            0.0f,
            0.0f,
            0.0f,
            range * zNearPlane,
            1.0f
        )

    member this.GetDeterminant() : float32 =
        let a = this.M11
        let b = this.M12
        let c = this.M13
        let d = this.M14
        let e = this.M21
        let f = this.M22
        let g = this.M23
        let h = this.M24
        let i = this.M31
        let j = this.M32
        let k = this.M33
        let l = this.M34
        let m = this.M41
        let n = this.M42
        let o = this.M43
        let p = this.M44

        let kp_lo = k * p - l * o
        let jp_ln = j * p - l * n
        let jo_kn = j * o - k * n
        let ip_lm = i * p - l * m
        let io_km = i * o - k * m
        let in_jm = i * n - j * m

        a * (f * kp_lo - g * jp_ln + h * jo_kn)
        - b * (e * kp_lo - g * ip_lm + h * io_km)
        + c * (e * jp_ln - f * ip_lm + h * in_jm)
        - d * (e * jo_kn - f * io_km + g * in_jm)

    /// The inverse by cofactors, or None when the determinant is too small to divide by.
    static member TryInvert(matrix: Matrix4x4) : Matrix4x4 option =
        let a = matrix.M11
        let b = matrix.M12
        let c = matrix.M13
        let d = matrix.M14
        let e = matrix.M21
        let f = matrix.M22
        let g = matrix.M23
        let h = matrix.M24
        let i = matrix.M31
        let j = matrix.M32
        let k = matrix.M33
        let l = matrix.M34
        let m = matrix.M41
        let n = matrix.M42
        let o = matrix.M43
        let p = matrix.M44

        let kp_lo = k * p - l * o
        let jp_ln = j * p - l * n
        let jo_kn = j * o - k * n
        let ip_lm = i * p - l * m
        let io_km = i * o - k * m
        let in_jm = i * n - j * m

        let a11 = f * kp_lo - g * jp_ln + h * jo_kn
        let a12 = -(e * kp_lo - g * ip_lm + h * io_km)
        let a13 = e * jp_ln - f * ip_lm + h * in_jm
        let a14 = -(e * jo_kn - f * io_km + g * in_jm)

        let det = a * a11 + b * a12 + c * a13 + d * a14

        if abs det < Single.Epsilon then
            None
        else
            let invDet = 1.0f / det

            let gp_ho = g * p - h * o
            let fp_hn = f * p - h * n
            let fo_gn = f * o - g * n
            let ep_hm = e * p - h * m
            let eo_gm = e * o - g * m
            let en_fm = e * n - f * m

            let gl_hk = g * l - h * k
            let fl_hj = f * l - h * j
            let fk_gj = f * k - g * j
            let el_hi = e * l - h * i
            let ek_gi = e * k - g * i
            let ej_fi = e * j - f * i

            Some(
                Matrix4x4(
                    a11 * invDet,
                    -(b * kp_lo - c * jp_ln + d * jo_kn) * invDet,
                    (b * gp_ho - c * fp_hn + d * fo_gn) * invDet,
                    -(b * gl_hk - c * fl_hj + d * fk_gj) * invDet,
                    a12 * invDet,
                    (a * kp_lo - c * ip_lm + d * io_km) * invDet,
                    -(a * gp_ho - c * ep_hm + d * eo_gm) * invDet,
                    (a * gl_hk - c * el_hi + d * ek_gi) * invDet,
                    a13 * invDet,
                    -(a * jp_ln - b * ip_lm + d * in_jm) * invDet,
                    (a * fp_hn - b * ep_hm + d * en_fm) * invDet,
                    -(a * fl_hj - b * el_hi + d * ej_fi) * invDet,
                    a14 * invDet,
                    (a * jo_kn - b * io_km + c * in_jm) * invDet,
                    -(a * fo_gn - b * eo_gm + c * en_fm) * invDet,
                    (a * fk_gj - b * ek_gi + c * ej_fi) * invDet
                )
            )

    override this.Equals(other: obj) =
        match other with
        | :? Matrix4x4 as other ->
            this.M11 = other.M11
            && this.M22 = other.M22
            && this.M33 = other.M33
            && this.M44 = other.M44
            && this.M12 = other.M12
            && this.M13 = other.M13
            && this.M14 = other.M14
            && this.M21 = other.M21
            && this.M23 = other.M23
            && this.M24 = other.M24
            && this.M31 = other.M31
            && this.M32 = other.M32
            && this.M34 = other.M34
            && this.M41 = other.M41
            && this.M42 = other.M42
            && this.M43 = other.M43
        | _ -> false

    override this.GetHashCode() =
        let mutable hash = this.M11.GetHashCode()
        hash <- (hash * 397) ^^^ this.M12.GetHashCode()
        hash <- (hash * 397) ^^^ this.M13.GetHashCode()
        hash <- (hash * 397) ^^^ this.M14.GetHashCode()
        hash <- (hash * 397) ^^^ this.M21.GetHashCode()
        hash <- (hash * 397) ^^^ this.M22.GetHashCode()
        hash <- (hash * 397) ^^^ this.M23.GetHashCode()
        hash <- (hash * 397) ^^^ this.M24.GetHashCode()
        hash <- (hash * 397) ^^^ this.M31.GetHashCode()
        hash <- (hash * 397) ^^^ this.M32.GetHashCode()
        hash <- (hash * 397) ^^^ this.M33.GetHashCode()
        hash <- (hash * 397) ^^^ this.M34.GetHashCode()
        hash <- (hash * 397) ^^^ this.M41.GetHashCode()
        hash <- (hash * 397) ^^^ this.M42.GetHashCode()
        hash <- (hash * 397) ^^^ this.M43.GetHashCode()
        hash <- (hash * 397) ^^^ this.M44.GetHashCode()
        hash

    override this.ToString() =
        "{ {M11:"
        + this.M11.ToString()
        + " M12:"
        + this.M12.ToString()
        + " M13:"
        + this.M13.ToString()
        + " M14:"
        + this.M14.ToString()
        + "} {M21:"
        + this.M21.ToString()
        + " M22:"
        + this.M22.ToString()
        + " M23:"
        + this.M23.ToString()
        + " M24:"
        + this.M24.ToString()
        + "} {M31:"
        + this.M31.ToString()
        + " M32:"
        + this.M32.ToString()
        + " M33:"
        + this.M33.ToString()
        + " M34:"
        + this.M34.ToString()
        + "} {M41:"
        + this.M41.ToString()
        + " M42:"
        + this.M42.ToString()
        + " M43:"
        + this.M43.ToString()
        + " M44:"
        + this.M44.ToString()
        + "} }"

    interface IEquatable<Matrix4x4> with
        member this.Equals(other: Matrix4x4) = this.Equals(box other)

module Matrix4x4Helpers =
    /// Matrix4x4.Invert(matrix, out result): the compiler passes the out argument as a reference cell.
    let invert (matrix: Matrix4x4) (result: Matrix4x4 ref) : bool =
        match Matrix4x4.TryInvert matrix with
        | Some inverse ->
            result.Value <- inverse
            true
        | None ->
            let nan = Single.NaN
            result.Value <- Matrix4x4(nan, nan, nan, nan, nan, nan, nan, nan, nan, nan, nan, nan, nan, nan, nan, nan)
            false

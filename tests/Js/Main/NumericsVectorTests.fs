module Fable.Tests.NumericsVectors

open System
open System.Numerics
open Util.Testing
open Fable.Tests.Util

// System.Numerics.Vector2, Vector3, Vector4 and Matrix4x4

// MathF.Sin, MathF.Cos... can differ in the last digits between runtimes
let private close (expected: float32) (actual: float32) =
    if abs (expected - actual) > 1e-5f * max 1.0f (abs expected) then
        failwith $"Expected {expected} but got {actual}"

let private closeV3 (x: float32, y: float32, z: float32) (v: Vector3) =
    close x v.X
    close y v.Y
    close z v.Z

let private cells (m: Matrix4x4) =
    [|
        m.M11; m.M12; m.M13; m.M14
        m.M21; m.M22; m.M23; m.M24
        m.M31; m.M32; m.M33; m.M34
        m.M41; m.M42; m.M43; m.M44
    |]

let private closeM (expected: float32[]) (m: Matrix4x4) =
    Array.iter2 close expected (cells m)

[<Struct>]
type private Particle =
    val mutable Position: Vector3
    val mutable Velocity: Vector4
    val mutable Age: float32

let private half = MathF.PI / 2.0f

let tests =
  testList "NumericsVectors" [
    testCase "Vector constructors and fields work" <| fun () ->
        let v2 = Vector2(1.0f, 2.0f)
        (v2.X, v2.Y) |> equal (1.0f, 2.0f)
        let v3 = Vector3(1.0f, 2.0f, 3.0f)
        (v3.X, v3.Y, v3.Z) |> equal (1.0f, 2.0f, 3.0f)
        let v4 = Vector4(1.0f, 2.0f, 3.0f, 4.0f)
        (v4.X, v4.Y, v4.Z, v4.W) |> equal (1.0f, 2.0f, 3.0f, 4.0f)

    testCase "Vector constructors from a single value and from smaller vectors work" <| fun () ->
        let v2 = Vector2(7.0f)
        (v2.X, v2.Y) |> equal (7.0f, 7.0f)
        let v3 = Vector3(7.0f)
        (v3.X, v3.Y, v3.Z) |> equal (7.0f, 7.0f, 7.0f)
        let fromV2 = Vector3(Vector2(1.0f, 2.0f), 3.0f)
        (fromV2.X, fromV2.Y, fromV2.Z) |> equal (1.0f, 2.0f, 3.0f)
        let v4 = Vector4(Vector3(1.0f, 2.0f, 3.0f), 4.0f)
        (v4.X, v4.Y, v4.Z, v4.W) |> equal (1.0f, 2.0f, 3.0f, 4.0f)
        let v4b = Vector4(Vector2(1.0f, 2.0f), 3.0f, 4.0f)
        (v4b.X, v4b.Y, v4b.Z, v4b.W) |> equal (1.0f, 2.0f, 3.0f, 4.0f)

    testCase "Vector constants work" <| fun () ->
        Vector3.Zero |> equal (Vector3(0.0f, 0.0f, 0.0f))
        Vector3.One |> equal (Vector3(1.0f, 1.0f, 1.0f))
        Vector3.UnitX |> equal (Vector3(1.0f, 0.0f, 0.0f))
        Vector3.UnitY |> equal (Vector3(0.0f, 1.0f, 0.0f))
        Vector3.UnitZ |> equal (Vector3(0.0f, 0.0f, 1.0f))
        Vector2.UnitY |> equal (Vector2(0.0f, 1.0f))
        Vector4.UnitW |> equal (Vector4(0.0f, 0.0f, 0.0f, 1.0f))
        Vector4.One |> equal (Vector4(1.0f))

    testCase "The default value of a vector is zero" <| fun () ->
        let v = Vector3()
        v |> equal Vector3.Zero
        Unchecked.defaultof<Vector4> |> equal Vector4.Zero
        let m = Matrix4x4()
        m.M11 |> equal 0.0f
        m.M44 |> equal 0.0f

    testCase "Vector fields of a struct default to zero" <| fun () ->
        let p = Particle()
        p.Position |> equal Vector3.Zero
        p.Velocity |> equal Vector4.Zero
        p.Age |> equal 0.0f

    testCase "Vector fields can be set" <| fun () ->
        let mutable v = Vector3(1.0f, 2.0f, 3.0f)
        v.X <- 10.0f
        v.Z <- v.Y + 1.0f
        v |> equal (Vector3(10.0f, 2.0f, 3.0f))
        let mutable p = Particle()
        p.Position <- Vector3(1.0f, 2.0f, 3.0f)
        p.Age <- 2.5f
        p.Position.Y |> equal 2.0f
        p.Age |> equal 2.5f

    testCase "Field assignments after a default struct give the assigned values" <| fun () ->
        let mutable p = Particle()
        p.Position <- Vector3(1.0f, 2.0f, 3.0f)
        p.Velocity <- Vector4(4.0f)
        p.Age <- 5.0f
        p.Position |> equal (Vector3(1.0f, 2.0f, 3.0f))
        p.Velocity |> equal (Vector4(4.0f))
        p.Age |> equal 5.0f
        let mutable partial = Particle()
        partial.Velocity <- Vector4.One
        partial.Position |> equal Vector3.Zero
        partial.Velocity |> equal Vector4.One
        partial.Age |> equal 0.0f

    testCase "Field assignments after a default struct keep their order of evaluation" <| fun () ->
        let log = ResizeArray<string>()

        let next (name: string) (value: 'T) =
            log.Add name
            value

        let mutable p = Particle()
        p.Age <- next "age" 1.0f
        p.Position <- next "position" Vector3.One
        p.Velocity <- next "velocity" Vector4.One
        List.ofSeq log |> equal [ "age"; "position"; "velocity" ]
        p.Age |> equal 1.0f
        p.Position |> equal Vector3.One
        p.Velocity |> equal Vector4.One

    testCase "A field assignment after a default struct can read the struct" <| fun () ->
        let mutable p = Particle()
        p.Position <- Vector3(2.0f, 0.0f, 0.0f)
        p.Age <- p.Position.X + p.Age + 1.0f
        p.Velocity <- Vector4(p.Position, p.Age)
        p.Age <- p.Age * 2.0f
        p.Age |> equal 6.0f
        p.Velocity |> equal (Vector4(2.0f, 0.0f, 0.0f, 3.0f))

    testCase "Vector3 arithmetic operators work" <| fun () ->
        let a = Vector3(1.0f, 2.0f, 3.0f)
        let b = Vector3(4.0f, 8.0f, 12.0f)
        a + b |> equal (Vector3(5.0f, 10.0f, 15.0f))
        b - a |> equal (Vector3(3.0f, 6.0f, 9.0f))
        a * b |> equal (Vector3(4.0f, 16.0f, 36.0f))
        a * 2.0f |> equal (Vector3(2.0f, 4.0f, 6.0f))
        2.0f * a |> equal (Vector3(2.0f, 4.0f, 6.0f))
        b / a |> equal (Vector3(4.0f, 4.0f, 4.0f))
        b / 4.0f |> equal (Vector3(1.0f, 2.0f, 3.0f))
        -a |> equal (Vector3(-1.0f, -2.0f, -3.0f))

    testCase "Vector2 and Vector4 arithmetic operators work" <| fun () ->
        Vector2(1.0f, 2.0f) + Vector2(3.0f, 4.0f) |> equal (Vector2(4.0f, 6.0f))
        Vector2(1.0f, 2.0f) * 3.0f |> equal (Vector2(3.0f, 6.0f))
        -Vector2(1.0f, 2.0f) |> equal (Vector2(-1.0f, -2.0f))
        Vector4(1.0f, 2.0f, 3.0f, 4.0f) - Vector4(1.0f) |> equal (Vector4(0.0f, 1.0f, 2.0f, 3.0f))
        Vector4(1.0f, 2.0f, 3.0f, 4.0f) * 2.0f |> equal (Vector4(2.0f, 4.0f, 6.0f, 8.0f))
        Vector4(2.0f, 4.0f, 6.0f, 8.0f) / 2.0f |> equal (Vector4(1.0f, 2.0f, 3.0f, 4.0f))

    testCase "Vector named arithmetic works" <| fun () ->
        let a = Vector3(1.0f, 2.0f, 3.0f)
        Vector3.Add(a, a) |> equal (Vector3(2.0f, 4.0f, 6.0f))
        Vector3.Subtract(a, a) |> equal Vector3.Zero
        Vector3.Multiply(a, 2.0f) |> equal (Vector3(2.0f, 4.0f, 6.0f))
        Vector3.Multiply(2.0f, a) |> equal (Vector3(2.0f, 4.0f, 6.0f))
        Vector3.Multiply(a, a) |> equal (Vector3(1.0f, 4.0f, 9.0f))
        Vector3.Divide(a, 2.0f) |> equal (Vector3(0.5f, 1.0f, 1.5f))
        Vector3.Negate a |> equal (Vector3(-1.0f, -2.0f, -3.0f))

    testCase "Vector3 Dot and Cross work" <| fun () ->
        Vector3.Dot(Vector3(1.0f, 2.0f, 3.0f), Vector3(4.0f, 5.0f, 6.0f)) |> equal 32.0f
        Vector3.Cross(Vector3.UnitX, Vector3.UnitY) |> equal Vector3.UnitZ
        Vector3.Cross(Vector3(1.0f, 2.0f, 3.0f), Vector3(4.0f, 5.0f, 6.0f)) |> equal (Vector3(-3.0f, 6.0f, -3.0f))
        Vector2.Dot(Vector2(1.0f, 2.0f), Vector2(3.0f, 4.0f)) |> equal 11.0f
        Vector4.Dot(Vector4(1.0f, 2.0f, 3.0f, 4.0f), Vector4(1.0f)) |> equal 10.0f

    testCase "Vector lengths and distances work" <| fun () ->
        let v = Vector3(2.0f, 3.0f, 6.0f)
        v.LengthSquared() |> equal 49.0f
        v.Length() |> equal 7.0f
        Vector2(3.0f, 4.0f).Length() |> equal 5.0f
        Vector4(1.0f, 1.0f, 1.0f, 1.0f).LengthSquared() |> equal 4.0f
        Vector3.Distance(Vector3(1.0f, 1.0f, 1.0f), Vector3(3.0f, 4.0f, 7.0f)) |> equal 7.0f
        Vector3.DistanceSquared(Vector3(1.0f, 1.0f, 1.0f), Vector3(3.0f, 4.0f, 7.0f)) |> equal 49.0f

    testCase "Vector Normalize works" <| fun () ->
        Vector3.Normalize(Vector3(0.0f, 3.0f, 4.0f)) |> closeV3 (0.0f, 0.6f, 0.8f)
        let n = Vector2.Normalize(Vector2(3.0f, 4.0f))
        close 0.6f n.X
        close 0.8f n.Y
        close 1.0f (Vector4.Normalize(Vector4(1.0f, 2.0f, 3.0f, 4.0f)).Length())

    testCase "Vector Lerp, Min, Max, Abs and Clamp work" <| fun () ->
        Vector3.Lerp(Vector3(0.0f, 2.0f, 4.0f), Vector3(4.0f, 6.0f, 8.0f), 0.25f) |> equal (Vector3(1.0f, 3.0f, 5.0f))
        Vector3.Min(Vector3(1.0f, 5.0f, 3.0f), Vector3(4.0f, 2.0f, 6.0f)) |> equal (Vector3(1.0f, 2.0f, 3.0f))
        Vector3.Max(Vector3(1.0f, 5.0f, 3.0f), Vector3(4.0f, 2.0f, 6.0f)) |> equal (Vector3(4.0f, 5.0f, 6.0f))
        Vector3.Abs(Vector3(-1.0f, 2.0f, -3.0f)) |> equal (Vector3(1.0f, 2.0f, 3.0f))
        Vector3.Clamp(Vector3(-1.0f, 0.5f, 3.0f), Vector3.Zero, Vector3.One) |> equal (Vector3(0.0f, 0.5f, 1.0f))
        Vector3.SquareRoot(Vector3(4.0f, 9.0f, 16.0f)) |> equal (Vector3(2.0f, 3.0f, 4.0f))
        Vector4.Lerp(Vector4.Zero, Vector4(4.0f), 0.5f) |> equal (Vector4(2.0f))

    testCase "Vector3 Reflect works" <| fun () ->
        Vector3.Reflect(Vector3(1.0f, -1.0f, 0.0f), Vector3.UnitY) |> equal (Vector3(1.0f, 1.0f, 0.0f))

    testCase "Vector equality and hashing work" <| fun () ->
        let a = Vector3(1.0f, 2.0f, 3.0f)
        let b = Vector3(1.0f, 2.0f, 3.0f)
        let c = Vector3(1.0f, 2.0f, 4.0f)
        a = b |> equal true
        a <> c |> equal true
        a.Equals(b) |> equal true
        a.Equals(c) |> equal false
        a.GetHashCode() = b.GetHashCode() |> equal true
        Vector2(1.0f, 2.0f) = Vector2(1.0f, 2.0f) |> equal true
        Vector4(1.0f) = Vector4(2.0f) |> equal false
        let set = System.Collections.Generic.HashSet<Vector3>()
        set.Add a |> equal true
        set.Add b |> equal false
        set.Contains c |> equal false

    testCase "Matrix4x4 constructor, fields and Identity work" <| fun () ->
        let m =
            Matrix4x4(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f)
        m.M11 |> equal 1.0f
        m.M12 |> equal 2.0f
        m.M21 |> equal 5.0f
        m.M34 |> equal 12.0f
        m.M44 |> equal 16.0f
        m.IsIdentity |> equal false
        Matrix4x4.Identity.IsIdentity |> equal true
        Matrix4x4.Identity.M22 |> equal 1.0f
        Matrix4x4.Identity.M23 |> equal 0.0f

    testCase "Matrix4x4 fields can be set" <| fun () ->
        let mutable m = Matrix4x4.Identity
        m.M41 <- 5.0f
        m.M11 <- m.M41 * 2.0f
        m.M41 |> equal 5.0f
        m.M11 |> equal 10.0f

    testCase "Matrix4x4 translation and scale work" <| fun () ->
        let t = Matrix4x4.CreateTranslation(Vector3(1.0f, 2.0f, 3.0f))
        t.Translation |> equal (Vector3(1.0f, 2.0f, 3.0f))
        Matrix4x4.CreateTranslation(1.0f, 2.0f, 3.0f) |> equal t
        Vector3.Transform(Vector3(10.0f, 20.0f, 30.0f), t) |> equal (Vector3(11.0f, 22.0f, 33.0f))
        let s = Matrix4x4.CreateScale(Vector3(2.0f, 3.0f, 4.0f))
        Vector3.Transform(Vector3(1.0f, 1.0f, 1.0f), s) |> equal (Vector3(2.0f, 3.0f, 4.0f))
        Matrix4x4.CreateScale(2.0f, 3.0f, 4.0f) |> equal s
        Matrix4x4.CreateScale(2.0f) |> equal (Matrix4x4.CreateScale(Vector3(2.0f)))
        let centered = Matrix4x4.CreateScale(2.0f, Vector3(1.0f, 1.0f, 1.0f))
        Vector3.Transform(Vector3(2.0f, 1.0f, 0.0f), centered) |> equal (Vector3(3.0f, 1.0f, -1.0f))

    testCase "Matrix4x4 Translation can be set" <| fun () ->
        let mutable m = Matrix4x4.Identity
        m.Translation <- Vector3(4.0f, 5.0f, 6.0f)
        (m.M41, m.M42, m.M43) |> equal (4.0f, 5.0f, 6.0f)

    testCase "Matrix4x4 multiplication applies the left matrix first" <| fun () ->
        let scaleThenMove =
            Matrix4x4.CreateScale(2.0f) * Matrix4x4.CreateTranslation(Vector3(1.0f, 0.0f, 0.0f))
        Vector3.Transform(Vector3(1.0f, 1.0f, 1.0f), scaleThenMove) |> equal (Vector3(3.0f, 2.0f, 2.0f))
        let moveThenScale =
            Matrix4x4.CreateTranslation(Vector3(1.0f, 0.0f, 0.0f)) * Matrix4x4.CreateScale(2.0f)
        Vector3.Transform(Vector3(1.0f, 1.0f, 1.0f), moveThenScale) |> equal (Vector3(4.0f, 2.0f, 2.0f))
        Matrix4x4.Multiply(Matrix4x4.CreateScale(2.0f), Matrix4x4.Identity) |> equal (Matrix4x4.CreateScale(2.0f))

    testCase "Matrix4x4 addition, subtraction, negation and scalar multiplication work" <| fun () ->
        let i = Matrix4x4.Identity
        (i + i).M11 |> equal 2.0f
        (i - i).M11 |> equal 0.0f
        (-i).M22 |> equal -1.0f
        (i * 3.0f).M33 |> equal 3.0f
        (i * 3.0f).M12 |> equal 0.0f
        Matrix4x4.Lerp(i, i * 3.0f, 0.5f).M11 |> equal 2.0f

    testCase "Matrix4x4 rotations work" <| fun () ->
        Vector3.Transform(Vector3.UnitY, Matrix4x4.CreateRotationX half) |> closeV3 (0.0f, 0.0f, 1.0f)
        Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateRotationY half) |> closeV3 (1.0f, 0.0f, 0.0f)
        Vector3.Transform(Vector3.UnitX, Matrix4x4.CreateRotationZ half) |> closeV3 (0.0f, 1.0f, 0.0f)
        Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, 0.7f) |> closeM (cells (Matrix4x4.CreateRotationY 0.7f))
        Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, 0.7f) |> closeM (cells (Matrix4x4.CreateRotationX 0.7f))
        Matrix4x4.CreateFromAxisAngle(Vector3.UnitZ, 0.7f) |> closeM (cells (Matrix4x4.CreateRotationZ 0.7f))

    testCase "Vector transforms work" <| fun () ->
        let m = Matrix4x4.CreateScale(2.0f) * Matrix4x4.CreateTranslation(Vector3(1.0f, 2.0f, 3.0f))
        Vector3.TransformNormal(Vector3(1.0f, 1.0f, 1.0f), m) |> equal (Vector3(2.0f, 2.0f, 2.0f))
        Vector4.Transform(Vector4(1.0f, 1.0f, 1.0f, 1.0f), m) |> equal (Vector4(3.0f, 4.0f, 5.0f, 1.0f))
        Vector4.Transform(Vector4(1.0f, 1.0f, 1.0f, 0.0f), m) |> equal (Vector4(2.0f, 2.0f, 2.0f, 0.0f))
        Vector4.Transform(Vector3(1.0f, 1.0f, 1.0f), m) |> equal (Vector4(3.0f, 4.0f, 5.0f, 1.0f))
        Vector2.Transform(Vector2(1.0f, 1.0f), m) |> equal (Vector2(3.0f, 4.0f))
        Vector2.TransformNormal(Vector2(1.0f, 1.0f), m) |> equal (Vector2(2.0f, 2.0f))

    testCase "Matrix4x4 CreateLookAt works" <| fun () ->
        let view = Matrix4x4.CreateLookAt(Vector3(0.0f, 0.0f, 5.0f), Vector3.Zero, Vector3.UnitY)
        Vector3.Transform(Vector3.Zero, view) |> closeV3 (0.0f, 0.0f, -5.0f)
        Vector3.Transform(Vector3(1.0f, 2.0f, 0.0f), view) |> closeV3 (1.0f, 2.0f, -5.0f)

    testCase "Matrix4x4 projections work" <| fun () ->
        let p = Matrix4x4.CreatePerspectiveFieldOfView(half, 2.0f, 1.0f, 3.0f)
        closeM [| 0.5f; 0.0f; 0.0f; 0.0f; 0.0f; 1.0f; 0.0f; 0.0f; 0.0f; 0.0f; -1.5f; -1.0f; 0.0f; 0.0f; -1.5f; 0.0f |] p
        let o = Matrix4x4.CreateOrthographic(4.0f, 2.0f, 1.0f, 3.0f)
        closeM [| 0.5f; 0.0f; 0.0f; 0.0f; 0.0f; 1.0f; 0.0f; 0.0f; 0.0f; 0.0f; -0.5f; 0.0f; 0.0f; 0.0f; -0.5f; 1.0f |] o
        let w = Matrix4x4.CreatePerspective(4.0f, 2.0f, 1.0f, 3.0f)
        closeM [| 0.5f; 0.0f; 0.0f; 0.0f; 0.0f; 1.0f; 0.0f; 0.0f; 0.0f; 0.0f; -1.5f; -1.0f; 0.0f; 0.0f; -1.5f; 0.0f |] w

    testCase "Matrix4x4 CreatePerspectiveFieldOfView rejects a bad field of view" <| fun () ->
        throwsAnyError (fun () -> Matrix4x4.CreatePerspectiveFieldOfView(0.0f, 1.0f, 1.0f, 2.0f))
        throwsAnyError (fun () -> Matrix4x4.CreatePerspectiveFieldOfView(1.0f, 1.0f, 2.0f, 1.0f))

    testCase "Matrix4x4 Transpose and GetDeterminant work" <| fun () ->
        let m =
            Matrix4x4(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f, 13.0f, 14.0f, 15.0f, 16.0f)
        let t = Matrix4x4.Transpose m
        (t.M12, t.M21, t.M14, t.M41) |> equal (5.0f, 2.0f, 13.0f, 4.0f)
        m.GetDeterminant() |> equal 0.0f
        Matrix4x4.CreateScale(2.0f, 3.0f, 4.0f).GetDeterminant() |> equal 24.0f

    testCase "Matrix4x4 Invert works" <| fun () ->
        let m =
            Matrix4x4.CreateScale(2.0f, 4.0f, 8.0f)
            * Matrix4x4.CreateRotationY 0.5f
            * Matrix4x4.CreateTranslation(Vector3(1.0f, 2.0f, 3.0f))
        let mutable inverse = Matrix4x4.Identity
        Matrix4x4.Invert(m, &inverse) |> equal true
        m * inverse |> closeM (cells Matrix4x4.Identity)
        let point = Vector3(3.0f, -2.0f, 5.0f)
        let back = Vector3.Transform(Vector3.Transform(point, m), inverse)
        closeV3 (3.0f, -2.0f, 5.0f) back

    testCase "Matrix4x4 Invert reports a singular matrix" <| fun () ->
        let singular = Matrix4x4.CreateScale(1.0f, 0.0f, 1.0f)
        let mutable inverse = Matrix4x4.Identity
        Matrix4x4.Invert(singular, &inverse) |> equal false
        Single.IsNaN inverse.M11 |> equal true
        let ok, _ = Matrix4x4.Invert(Matrix4x4.Identity)
        ok |> equal true

    testCase "Matrix4x4 equality works" <| fun () ->
        Matrix4x4.Identity = Matrix4x4.Identity |> equal true
        Matrix4x4.Identity = Matrix4x4.CreateScale(2.0f) |> equal false
        Matrix4x4.Identity <> Matrix4x4.CreateScale(2.0f) |> equal true
        Matrix4x4.Identity.GetHashCode() = Matrix4x4.Identity.GetHashCode() |> equal true

    testCase "Single-precision square roots and powers are rounded to single precision" <| fun () ->
        // The double square root of 14 is 3.7416573867739413; the float32 one widens to the value below
        float (MathF.Sqrt 14.0f) |> equal 3.7416574954986572
        float (sqrt 14.0f) |> equal 3.7416574954986572
        float (MathF.Pow(2.0f, 0.5f)) |> equal 1.4142135381698608
        float (MathF.Exp 1.0f) = Math.Exp 1.0 |> equal false

    testCase "Vector3 Normalize gives the single-precision result" <| fun () ->
        let n = Vector3.Normalize(Vector3(1.0f, 2.0f, 3.0f))
        float n.Z |> equal 0.8017836809158325
        float (Vector3(1.0f, 2.0f, 3.0f).Length()) |> equal 3.7416574954986572

    testCase "Vector arithmetic is rounded to single precision" <| fun () ->
        // 0.1f + 0.2f differs from the double sum; the result must be the float32 one
        let v = Vector2(0.1f, 0.2f) + Vector2(0.2f, 0.1f)
        v.X |> equal (0.1f + 0.2f)
        float v.X = 0.1 + 0.2 |> equal false
  ]

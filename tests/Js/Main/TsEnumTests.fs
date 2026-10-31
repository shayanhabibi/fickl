module Fable.Tests.TsEnums

open Util.Testing
open Fable.Core
open Fickl.Core
#if FABLE_COMPILER
open Fable.Core.JsInterop
#endif

[<TsEnum>]
type Color =
    | Red = 0
    | Green = 1
    | Blue = 4

[<TsEnum; System.Flags>]
type Access =
    | NoAccess = 0
    | Read = 1
    | Write = 2

let next (color: Color) =
    match color with
    | Color.Red -> Color.Green
    | Color.Green -> Color.Blue
    | _ -> Color.Red

// The imported enums are plain JavaScript objects, which TypeScript cannot use as types;
// tests/Integration/Integration/data/tsEnums type-checks them against a declared string enum
#if FABLE_COMPILER && !FABLE_COMPILER_TYPESCRIPT
[<Import("Dir", "./js/enums.js"); StringEnum>]
type Dir =
    | [<CompiledName("UP")>] Up
    | [<CompiledName("DOWN")>] Down

[<Import("Level", "./js/enums.js"); StringEnum>]
type Level =
    | [<CompiledName("low")>] Low
    | [<CompiledName("high")>] High

[<Import("move", "./js/enums.js")>]
let move (dir: Dir) : string = jsNative

/// The raw string, as JavaScript code would pass it
[<Import("rawHigh", "./js/enums.js")>]
let rawHigh () : Level = jsNative

let describe (level: Level) =
    match level with
    | Level.Low -> "low"
    | Level.High -> "high"
#endif

let tests =
    testList "TsEnums" [
        testCase "TsEnum values stay numbers" <| fun () ->
            int Color.Blue |> equal 4
            next Color.Green |> equal Color.Blue
            next (enum 9) |> equal Color.Red
            Access.Read ||| Access.Write |> int |> equal 3
            (Access.Read ||| Access.Write).HasFlag Access.Write |> equal true

        testCase "TsEnum values in collections" <| fun () ->
            [ Color.Red; Color.Blue ] |> List.map int |> equal [ 0; 4 ]
            Array.create 2 Color.Green |> Array.map next |> equal [| Color.Blue; Color.Blue |]

#if FABLE_COMPILER
        testCase "TsEnum emits an object of the enum's members" <| fun () ->
            let colorObject: obj = JsInterop.emitJsExpr () "Color"
            colorObject?Green |> equal (box 1)
            colorObject?Blue |> equal (box 4)
            JsInterop.emitJsExpr colorObject "$0[4]" |> equal "Blue"
            JsInterop.emitJsExpr colorObject "Object.isFrozen($0)" |> equal true

#endif
#if FABLE_COMPILER && !FABLE_COMPILER_TYPESCRIPT
        testCase "Imported string enum cases are members of the imported object" <| fun () ->
            move Dir.Up |> equal "moved UP"
            string Level.Low |> equal "low-from-object"
            describe (rawHigh ()) |> equal "high"
            describe Level.Low |> equal "low"
#endif
    ]

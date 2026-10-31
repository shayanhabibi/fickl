module Enums

open Fable.Core
open Fickl.Core

[<TsEnum>]
type Color =
    | Red = 0
    | Green = 1
    | Blue = 4

[<TsEnum; System.Flags>]
type Access =
    | Read = 1
    | Write = 2

/// Not opted in: stays a number
type Plain =
    | A = 0
    | B = 1

[<Import("Dir", "./dir.js"); StringEnum>]
type Dir =
    | [<CompiledName("UP")>] Up
    | [<CompiledName("DOWN")>] Down

let favourite: Color = Color.Green

let next (color: Color) =
    match color with
    | Color.Red -> Color.Green
    | Color.Green -> Color.Blue
    | _ -> Color.Red

let ofInt (n: int) : Color = enum n
let toInt (color: Color) = int color
let combine (a: Access) (b: Access) = a ||| b
let plain (p: Plain) = p
let colors = [ Color.Red; Color.Blue ]

[<Import("move", "./dir.js")>]
let move (dir: Dir) : string = jsNative

let goUp () = move Dir.Up

// A parameter typed by the imported enum is left out: its annotation depends on the compiler's global target,
// which the in-process snapshot tests can share
let isDown () = Dir.Down = (JsInterop.emitJsExpr () "'DOWN'")

let outside: Color = enum 5
let mixed = Color.Red ||| Color.Blue
let zero = Unchecked.defaultof<Color>
let zeros: Color[] = Array.zeroCreate 2
let parsed = System.Enum.Parse<Color>("Blue")
let isGreen (color: Color) = color = Color.Green
let label (color: Color) = string color

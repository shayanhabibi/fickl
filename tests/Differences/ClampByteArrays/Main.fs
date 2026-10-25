// Change: `--clampByteArrays`
// Fickl: Compiles byte arrays as `Uint8ClampedArray`, the type canvas pixel data uses, so they can be passed to it without a copy.
// Issues: 39
// Args: --clampByteArrays
module Main

open Fable.Core.JsInterop

let pixels = [| 0uy; 128uy; 255uy |]

Check.equal true (emitJsExpr pixels "$0 instanceof Uint8ClampedArray")
Check.equal 383 (Array.sumBy int pixels)

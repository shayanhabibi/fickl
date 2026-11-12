// Change: `Int32.Parse` with signs, radix prefixes and underscores
// Fickl: `Int32.Parse` rejects `0x`, `0b`, `0o` and `_` as .NET does, and with `HexNumber` takes neither a sign nor a `0x` prefix.
// Issues: 261
module Main

open System
open System.Globalization

let rejects (parse: unit -> int) =
    try
        parse () |> ignore
        false
    with _ ->
        true

Check.equal 31 (Int32.Parse(" 1F ", NumberStyles.HexNumber))
Check.equal true (rejects (fun () -> Int32.Parse("0x1F", NumberStyles.HexNumber)))
Check.equal true (rejects (fun () -> Int32.Parse("-1F", NumberStyles.HexNumber)))
Check.equal true (rejects (fun () -> Int32.Parse "0x1F"))
Check.equal true (rejects (fun () -> Int32.Parse "1_0"))
Check.equal true (rejects (fun () -> int "+0x11"))

// Change: `BigInteger.Parse` and `TryParse` with `NumberStyles`
// Fickl: `BigInteger.Parse` and `TryParse` honour the `NumberStyles` argument, reading `HexNumber` digits as two's complement, instead of ignoring it or throwing a TypeError.
// Issues: 262
module Main

open System.Globalization

Check.equal 31I (bigint.Parse("1F", NumberStyles.HexNumber))
Check.equal -1I (bigint.Parse("FF", NumberStyles.HexNumber))

let mutable n = 77I
let ok = bigint.TryParse(" -12 ", NumberStyles.Any, CultureInfo.InvariantCulture, &n)
Check.equal (true, -12I) (ok, n)

// Change: `%o` in printf formats
// Fickl: Formats in octal, as .NET does; the value is not printed in decimal.
// Issues: 16
module Main

Check.equal "10|37777777777|  100" (sprintf "%o|%o|%5o" 8 -1 64)

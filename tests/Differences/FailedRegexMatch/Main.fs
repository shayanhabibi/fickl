// Change: members of a failed `Regex` match
// Fickl: `Value`, `Index`, `Length` and `Groups` of a failed match give .NET's empty-match values instead of throwing.
// Issues: none
module Main

open System.Text.RegularExpressions

let m = Regex("^test/(.*?)$").Match("test")
Check.equal false m.Success
Check.equal "" m.Value
Check.equal 0 m.Index
Check.equal 0 m.Length
Check.equal 1 m.Groups.Count

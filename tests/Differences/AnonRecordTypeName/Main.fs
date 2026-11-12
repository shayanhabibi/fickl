// Change: `Name` and `FullName` of an anonymous record type
// Fickl: An anonymous record type has a non-empty Name and FullName that tell different anonymous record types apart.
// Issues: 275
module Main

let t = {| A = {| B = "content" |} |}.GetType()
let inner = (FSharp.Reflection.FSharpType.GetRecordFields t).[0].PropertyType

Check.equal false (System.String.IsNullOrEmpty t.FullName)
Check.equal false (t.FullName = inner.FullName)
Check.equal false (typeof<{| a: int |}>.FullName = typeof<{| b: int |}>.FullName)
Check.equal true (typeof<{| a: int |}>.Name.StartsWith("<>f__AnonymousType", System.StringComparison.Ordinal))

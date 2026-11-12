// Change: `Match.NextMatch()`
// Fickl: `Match.NextMatch()` is implemented, resuming the search after the current match.
// Issues: 343
module Main

open System.Text.RegularExpressions

let next = Regex("a").Match("xaya").NextMatch()

Check.equal true next.Success
Check.equal 3 next.Index
Check.equal false (next.NextMatch().Success)

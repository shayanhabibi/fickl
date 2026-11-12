// Change: out-of-range dates in `DateTime` parsing
// Fickl: `DateTime.TryParse` rejects a day or month out of range, such as `2020-02-30`, instead of rolling it over.
// Issues: 270
module Main

open System

Check.equal false (fst (DateTime.TryParse "2020-02-30"))
Check.equal false (fst (DateTime.TryParse "2020-13-01"))
Check.equal true (fst (DateTime.TryParse "2020-02-29"))

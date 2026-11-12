// Change: `ResizeArray.Sort`, `Array.Sort` and `Comparer<string>.Default` on strings
// Fickl: The .NET comparer paths order strings like `String.CompareTo`, so lower case sorts before upper case, while F#'s `compare` and `List.sort` stay ordinal.
// Issues: 271
module Main

open System.Collections.Generic

let li = ResizeArray [ "b"; "A"; "a"; "B" ]
li.Sort()
Check.equal [ "a"; "A"; "b"; "B" ] (List.ofSeq li)
Check.equal 3 (li.BinarySearch "B")

Check.equal true (Comparer<string>.Default.Compare("a", "Z") < 0)
Check.equal true (compare "a" "Z" > 0)

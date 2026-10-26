// Change: an explicit class inheriting a class with a primary constructor
// Fickl: The class extends its base, so `{ inherit Base("derived"); Extra = 5 }` has its `Name`.
// Issues: 96
module Main

type Base(name: string) =
    member _.Name = name

type Derived =
    inherit Base
    val Extra: int
    new() = { inherit Base("derived"); Extra = 5 }

let d = Derived()
Check.equal "derived" d.Name
Check.equal 5 d.Extra
Check.equal true (box d :? Base)

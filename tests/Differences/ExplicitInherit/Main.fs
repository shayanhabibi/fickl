// Change: an explicit class inheriting another explicit class
// Fickl: The class extends its base and keeps the base's fields, so `{ inherit Base("derived"); Extra = 5 }` has its `Name`.
// Issues: 87
module Main

type Base =
    val Name: string
    new(name) = { Name = name }

type Derived =
    inherit Base
    val Extra: int
    new() = { inherit Base("derived"); Extra = 5 }

let d = Derived()
Check.equal "derived" d.Name
Check.equal 5 d.Extra
Check.equal true (box d :? Base)

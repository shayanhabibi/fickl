// Change: `[<StructuredFormatDisplay>]`
// Fickl: `%A`, `%O`, `string` and interpolation print a record or union with its [<StructuredFormatDisplay>] format.
// Issues: 266
module Main

[<StructuredFormatDisplay("{Display}")>]
type Foo =
    { a: int }
    member this.Display = $"Foo {{ a = {this.a} }}"

let foo = { a = 1337 }
Check.equal "Foo { a = 1337 }" (sprintf "%A" foo)
Check.equal "Foo { a = 1337 }" (string foo)
Check.equal "Foo { a = 1337 }" $"{foo}"

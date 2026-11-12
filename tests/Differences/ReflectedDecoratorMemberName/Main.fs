// Change: `ReflectedDecorator` member names
// Fickl: The `MethodInfo` given to a `ReflectedDecorator` has the member's own name, not the mangled declaration name.
// Issues: 276
module Main

open Fable.Core

let names = ResizeArray<string>()

type NameAttribute() =
    inherit JS.ReflectedDecoratorAttribute()

    override _.Decorate(fn, info) =
        names.Add info.Name
        fn

[<Name>]
let ``decorated function`` () = 1

type Decorated() =
    [<Name>]
    static member StaticMember(x: int) = x + 1

Check.equal 1 (``decorated function`` ())
Check.equal 2 (Decorated.StaticMember 1)
Check.equal [ "decorated function"; "StaticMember" ] (List.ofSeq names)

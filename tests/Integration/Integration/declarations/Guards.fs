/// Type guards: the declarations narrow the guarded parameter, whatever F# sees the function return
module Guards

open Fickl.Core

type Pet =
    abstract name: string

[<TypePredicate("value", typeof<string>)>]
let isText (value: obj) : bool = value :? string

[<TypePredicate("value", typeof<Pet>)>]
let tryPet (value: obj) : Pet option =
    if Fable.Core.JsInterop.isIn "name" value then
        Some(unbox<Pet> value)
    else
        None

[<TypeAssertion("value", typeof<Pet>)>]
let assertPet (value: obj) : unit =
    if (tryPet value).IsNone then
        failwith "not a pet"

[<TypeAssertion("value", typeof<Pet>)>]
let checkPet (value: obj) : Result<Pet, exn> =
    match tryPet value with
    | Some pet -> Ok pet
    | None -> Error(exn "not a pet")

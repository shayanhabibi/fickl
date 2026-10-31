module TypeGuards

open Fable.Core
open Fickl.Core

type User =
    abstract name: string

[<Import("isUserLike", "./guards.js"); TypePredicate("value", typeof<User>)>]
let isUserLike (value: obj) : User option = jsNative

[<TypePredicate("x", typeof<string>)>]
let isText (x: obj) : bool = x :? string

[<TypePredicate("x", typeof<User>)>]
let tryUser (x: obj) : User option =
    if isNull x then None
    elif JsInterop.isIn "name" x then Some(unbox<User> x)
    else None

[<TypePredicate("x", typeof<User>)>]
let tryUserValue (label: string) (x: obj) : User voption =
    match tryUser x with
    | Some user when label <> "" -> ValueSome user
    | _ -> ValueNone

[<TypeAssertion("x", typeof<User>)>]
let assertUser (x: obj) : unit =
    if (tryUser x).IsNone then failwith "not a user"

[<TypeAssertion("x", typeof<User>)>]
let checkUser (x: obj) : Result<User, exn> =
    match tryUser x with
    | Some user -> Ok user
    | None -> Error(exn "not a user")

let greet (x: obj) =
    match tryUser x with
    | Some user -> "hi " + user.name
    | None -> "who?"

let greetLoaded (load: unit -> obj) =
    match tryUserValue "label" (load ()) with
    | ValueSome user -> user.name
    | ValueNone -> ""

let checkedName (x: obj) =
    match checkUser x with
    | Ok user -> user.name
    | Error e -> e.Message

let imported (x: obj) = isUserLike x |> Option.map _.name

let users (values: obj list) = values |> List.choose tryUser

type Validator(prefix: string) =
    [<TypePredicate("value", typeof<User>)>]
    member _.TryUser(value: obj) : User option =
        tryUser value |> Option.filter (fun user -> user.name.StartsWith prefix)

    [<TypePredicate("value", typeof<string>)>]
    static member IsText(value: obj) : string option =
        match value with
        | :? string as text -> Some text
        | _ -> None

let validatedName (validator: Validator) (value: obj) =
    match validator.TryUser value with
    | Some user -> user.name
    | None -> Validator.IsText value |> Option.defaultValue ""

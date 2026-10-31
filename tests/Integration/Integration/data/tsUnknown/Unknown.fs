module Unknown

open Fable.Core
open Fable.Core.JsInterop
open Fickl.Core

/// Parses JSON; TypeScript callers must narrow the result.
let parse (text: string) : unknown = JS.JSON.parse text

let anyValue (value: obj) : obj = value

let field (value: unknown) (name: string) : unknown = value?(name)

let describe (value: unknown) =
    match value with
    | :? string as s -> "string " + s
    | :? float as n -> "number " + string n
    | _ -> "other"

let sumOf (values: unknown list) : float = values |> List.sumBy unbox<float>

let tryFirst (values: unknown[]) : unknown option = Array.tryHead values

let onValue (handler: unknown -> unit) (value: obj) = handler value

let count () : unknown = box 1

let useResults () =
    let parsed = parse "{\"a\":1}"
    unbox<float> (field parsed "a") + unbox<float> (count ()) + sumOf [ box 1.; box 2. ]

type IBag =
    abstract Get: key: string -> unknown
    abstract Put: key: string * value: unknown -> unit
    abstract Size: unknown

let bag () =
    let items = JS.Constructors.Map.Create<string, obj>()

    { new IBag with
        member _.Get key = items.get key
        member _.Put(key, value) = items.set (key, value) |> ignore
        member _.Size = box items.size }

let readBag (b: IBag) = (b.Get "a")?length

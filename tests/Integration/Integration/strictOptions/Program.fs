module StrictOptions

open Fable.Core
open Fable.Core.JsInterop

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A, got %A" expected actual

let describe = function
    | None -> "None"
    | Some value -> if isNull value then "Some null" else "Some " + value

type Optional(?value: string) =
    member _.Initial = value
    member _.Read(?value: string) = describe value
    member _.Closure(?value: string) = fun () -> describe value
    member this.Forward(?value: string) = this.Read(?value = value)
    static member Static(?value: string) = describe value

type IOptional =
    abstract Read: ?value: string -> string

type OptionalImpl() =
    interface IOptional with
        member _.Read(?value) = describe value

[<AttachMembers>]
type Attached() =
    member _.Read(?value: string) = describe value
    member _.Name: string option = Some null

type Named() =
    [<NamedParams>]
    member _.Read(?value: string) = describe value

type IName =
    abstract Name: string option

type NameImpl(value: string option) =
    interface IName with
        member _.Name = value

let nullValue: string = null
let present = Some nullValue
let instance = Optional()
instance.Read(?value = present) |> equal "Some null"
instance.Read(value = nullValue) |> equal "Some null"
instance.Read(?value = Some nullValue) |> equal "Some null"
instance.Read(?value = None) |> equal "None"
instance.Read() |> equal "None"
instance.Read(value = "text") |> equal "Some text"
instance.Closure(?value = present) () |> equal "Some null"
instance.Closure() () |> equal "None"
instance.Forward(?value = present) |> equal "Some null"
instance.Forward() |> equal "None"
Optional.Static(?value = present) |> equal "Some null"
Optional.Static() |> equal "None"
Optional(?value = present).Initial |> equal present
Optional().Initial |> equal None
(OptionalImpl() :> IOptional).Read(?value = present) |> equal "Some null"
(OptionalImpl() :> IOptional).Read() |> equal "None"
Attached().Read(?value = present) |> equal "Some null"
Attached().Read() |> equal "None"
Attached().Name |> equal present
(NameImpl(present) :> IName).Name |> equal present
{ new IName with member _.Name = present }.Name |> equal present
let anonymous = {| Name = present; Empty = (None: string option) |}
anonymous.Name |> equal present
anonymous.Empty |> equal None
Named().Read(?value = present) |> equal "Some null"
Named().Read(value = nullValue) |> equal "Some null"
Named().Read() |> equal "None"

#if FABLE_COMPILER
// Native bindings must still receive the raw value, including option variables.
type Binding =
    abstract Read: ?value: string -> string

let binding: Binding = import "binding" "./binding.js"
binding.Read(?value = present) |> equal "null"
binding.Read() |> equal "undefined"
binding.Read(value = "text") |> equal "text"
#endif

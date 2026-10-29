module EmitMemberSignatures

open Fable.Core

type Widget =
    abstract name: string

type Headers =
    [<EmitIndexer>]
    abstract Item: string -> string with get, set

type ReadOnlyHeaders =
    [<EmitIndexer>]
    abstract Item: int -> string

type WidgetCtor =
    [<EmitConstructor>]
    abstract Create: name: string -> Widget

    abstract version: string

type Api =
    [<EmitMethod("doIt")>]
    abstract DoIt: int -> int

    [<EmitProperty("the-name")>]
    abstract Name: string

    [<Emit("$0.raw")>]
    abstract Raw: int

let useAll (h: Headers) (r: ReadOnlyHeaders) (c: WidgetCtor) (a: Api) =
    h["x"] <- "y"
    h["x"], r[0], c.Create("w").name, a.DoIt 1, a.Name

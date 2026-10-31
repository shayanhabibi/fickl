module ReturnsThis

open Fickl.Core

type IQuery =
    [<ReturnsThis>]
    abstract where: string -> IQuery

    [<ReturnsThis>]
    abstract limit: int -> IQuery

    abstract text: unit -> string

type ISelect =
    inherit IQuery

    [<ReturnsThis>]
    abstract columns: string[] -> ISelect

type Select() =
    let mutable parts: string list = []

    interface ISelect with
        member this.where condition =
            parts <- ("where " + condition) :: parts
            this

        member this.limit count =
            if count = 0 then
                this
            else
                parts <- ("limit " + string count) :: parts
                this

        member this.columns names =
            parts <- ("select " + String.concat ", " names) :: parts
            this

        member _.text() = String.concat " " (List.rev parts)

[<AbstractClass>]
type Counter() =
    [<ReturnsThis>]
    abstract add: int -> Counter

type StepCounter() =
    inherit Counter()
    override this.add _ = this

let literal: ISelect =
    { new ISelect with
        member this.where _ = this
        member this.limit _ = this
        member this.columns _ = this
        member _.text() = ""
    }

let counter: Counter =
    { new Counter() with
        override this.add _ = this
    }

let select () : ISelect = Select()

let chained (query: ISelect) =
    query |> Fluent.chain (fun q -> q.where "x") |> Fluent.chain (fun q -> q.columns [| "a" |])

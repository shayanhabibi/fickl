module Fable.Tests.ReturnsThis

open Util.Testing
open Fable.Tests.Util
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
    abstract columns: string list -> ISelect

type Select() =
    let mutable parts: string list = []

    interface ISelect with
        member this.where condition =
            parts <- parts @ [ "where " + condition ]
            this

        member this.limit count =
            match count with
            | 0 -> this
            | _ ->
                parts <- parts @ [ "limit " + string count ]
                this

        member this.columns names =
            parts <- parts @ [ "select " + String.concat ", " names ]
            this

        member _.text() = String.concat " " parts

[<AbstractClass>]
type Counter() =
    member val Count = 0 with get, set

    [<ReturnsThis>]
    abstract add: int -> Counter

type StepCounter(step: int) =
    inherit Counter()

    override this.add times =
        this.Count <- this.Count + step * times
        this

let literalSelect () =
    let parts = ResizeArray<string>()

    { new ISelect with
        member this.where condition =
            parts.Add("where " + condition)
            this

        member this.limit count =
            parts.Add("limit " + string count)
            this

        member this.columns names =
            parts.Add("select " + String.concat ", " names)
            this

        member _.text() = String.concat " " parts
    }

let tests =
    testList "ReturnsThis" [
        testCase "class implementations return their receiver" <| fun () ->
            let select = Select() :> ISelect
            select.columns([ "a"; "b" ]).where("a > 1").limit(5).text () |> equal "select a, b where a > 1 limit 5"

        testCase "Fluent.chain keeps the receiver's type" <| fun () ->
            let select =
                Select() :> ISelect
                |> Fluent.chain (fun s -> s.where "x")
                |> Fluent.chain (fun s -> s.limit 0)
                |> fun s -> s.columns [ "c" ]

            select.text () |> equal "where x select c"

        testCase "object expressions implement ReturnsThis members" <| fun () ->
            let select = literalSelect ()
            let same = select |> Fluent.chain (fun s -> s.where "y")
            obj.ReferenceEquals(same, select) |> equal true
            same.columns([ "d" ]).text () |> equal "where y select d"

        testCase "overrides of ReturnsThis members return their receiver" <| fun () ->
            let counter = StepCounter(2)
            counter.add(1).add (3) |> ignore
            counter.Count |> equal 8

        testCase "object expressions overriding ReturnsThis members return their receiver" <| fun () ->
            let counter =
                { new Counter() with
                    override this.add times =
                        this.Count <- this.Count + times
                        this
                }

            counter.add(4).add(1).Count |> equal 5
    ]

// Change: `[<ReturnsThis>]`
// Fickl: An abstract method returning its own interface is declared returning `this` in TypeScript, so a subinterface keeps its members through a chain of calls; the JavaScript is unchanged.
// Issues: 152
// Args: --declaration
module Main

open Fable.Core
open Fickl.Core

[<Import("readFileSync", "node:fs")>]
let readFileSync (path: obj, encoding: string) : string = jsNative

[<Emit("new URL($0, import.meta.url)")>]
let besideThisFile (name: string) : obj = jsNative

type IQuery =
    [<ReturnsThis>]
    abstract where: string -> IQuery

type ISelect =
    inherit IQuery
    abstract columns: string -> ISelect

type Select() =
    member val Text = "" with get, set

    interface ISelect with
        member this.where condition =
            this.Text <- this.Text + " where " + condition
            this

        member this.columns names =
            this.Text <- "select " + names
            this

let select = Select()
let query = select :> ISelect |> Fluent.chain (fun q -> q.columns "a") |> Fluent.chain (fun q -> q.where "b")

let declaration = readFileSync (besideThisFile "Main.d.ts", "utf8")

Check.equal "select a where b" select.Text
Check.equal true (obj.ReferenceEquals(query, select))
Check.equal true (declaration.Contains "where(arg0: string): this")

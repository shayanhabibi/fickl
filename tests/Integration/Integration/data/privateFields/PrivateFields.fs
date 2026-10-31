module PrivateFields

open Fable.Core
open Fickl.Core

[<Global>]
type HTMLElement() =
    class
    end

type Counter() =
    inherit HTMLElement()
    let mutable title = "counter"
    let mutable hidden = 0
    member _.Title = title
    member _.Hide() = hidden <- hidden + 1

[<PrivateFields>]
type Account(balance: float) =
    let mutable balance = balance
    member _.Balance = balance
    member _.Deposit(amount: float) = balance <- balance + amount

[<PrivateFields(false)>]
type PublicFields() =
    inherit HTMLElement()
    let label = "public"
    member _.Label = label

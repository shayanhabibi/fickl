// Change: `--privateFields`
// Fickl: Compiles the `let` fields of every class with attached members to ES `#private` fields, which JavaScript cannot read or enumerate.
// Issues: 161
// Args: --privateFields
module Main

open Fable.Core
open Fable.Core.JsInterop

[<AttachMembers>]
type Account(balance: float) =
    let mutable balance = balance
    member _.Balance = balance
    member _.Deposit(amount: float) = balance <- balance + amount

let account = Account(10.)
account.Deposit 5.
Check.equal 15. account.Balance
Check.equal 0 (emitJsExpr account "Object.keys($0).length")

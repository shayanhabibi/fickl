// Change: `--temporal`
// Fickl: Represents dates and times with the JavaScript Temporal API, a documented flag for Fable's hidden `--test:js-temporal`.
// Issues: 39
// Args: --temporal
module Main

open System
open Fable.Core.JsInterop

let span = TimeSpan.FromSeconds 90.
let date = DateTime(2020, 1, 2)

Check.equal true (emitJsExpr span "$0 instanceof Temporal.Duration")
Check.equal "PlainDateTime" (emitJsExpr date "$0.constructor.name")
Check.equal "00:01:30" (string span)
Check.equal "2020-01-02" (date.ToString("yyyy-MM-dd"))

// Change: `[<CallableInterface>]`
// Fickl: An object expression implementing a callable interface compiles to a plain JavaScript function, and keeps a generic `Invoke` generic.
// Issues: 8, 10, 11
module Main

open Fable.Core.JsInterop
open Fickl.Core

[<CallableInterface>]
type Identity =
    abstract Invoke<'T>: value: 'T -> 'T

let identity =
    { new Identity with
        member _.Invoke value = value
    }

Check.equal "function" (jsTypeof identity)
Check.equal 3 (identity.Invoke 3)
Check.equal "a" (identity.Invoke "a")
